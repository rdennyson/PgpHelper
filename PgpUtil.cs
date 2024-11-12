namespace PgpHelper
{
    using Org.BouncyCastle.Bcpg;
    using Org.BouncyCastle.Bcpg.OpenPgp;
    using Org.BouncyCastle.Security;
    using Org.BouncyCastle.Utilities.IO;
    using System;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;

    public static class  PgpUtil
    {
        // Load the PGP public key from a stream
        private static PgpPublicKey ReadPublicKey(Stream inputStream)
        {
            try
            {
                // Use BouncyCastle PGP object factory to read the public key
                PgpPublicKeyRingBundle publicKeyRingBundle = new PgpPublicKeyRingBundle(PgpUtilities.GetDecoderStream(inputStream));

                // Iterate through the keys in the bundle and print details
                foreach (PgpPublicKeyRing keyRing in publicKeyRingBundle.GetKeyRings())
                {
                    foreach (PgpPublicKey key in keyRing.GetPublicKeys())
                    {
                        if (key.IsEncryptionKey)
                            return key;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading PGP public key: {ex.Message}");
            }
            throw new ArgumentException("No encryption key found in the public key ring.");
        }

        // Encrypt the file using the public key
        private static void EncryptFile(string inputFilePath, string outputFilePath, PgpPublicKey publicKey)
        {
            using (FileStream outputFileStream = new FileStream(outputFilePath, FileMode.Create))
            using (Stream outputStream = new ArmoredOutputStream(outputFileStream))
            {
                byte[] bytes = CompressFile(inputFilePath);

                PgpEncryptedDataGenerator encGen = new PgpEncryptedDataGenerator(SymmetricKeyAlgorithmTag.Cast5, true, new SecureRandom());
                encGen.AddMethod(publicKey);

                using (Stream encryptedOut = encGen.Open(outputStream, bytes.Length))
                {
                    encryptedOut.Write(bytes, 0, bytes.Length);
                }
            }
        }

        // Compress the file
        private static byte[] CompressFile(string fileName)
        {
            MemoryStream bOut = new MemoryStream();
            PgpCompressedDataGenerator comData = new PgpCompressedDataGenerator(CompressionAlgorithmTag.Zip);

            using (Stream cos = comData.Open(bOut))
            {
                PgpLiteralDataGenerator lData = new PgpLiteralDataGenerator();
                FileInfo file = new FileInfo(fileName);

                using (Stream pOut = lData.Open(cos, PgpLiteralData.Binary, file))
                using (FileStream inputFile = file.OpenRead())
                {
                    Streams.PipeAll(inputFile, pOut);
                }
            }
            return bOut.ToArray();
        }
        public static PgpPrivateKey FindSecretKey(PgpSecretKeyRingBundle pgpSec, long keyId, char[] passphrase)
        {
            PgpSecretKey pgpSecKey = pgpSec.GetSecretKey(keyId);

            if (pgpSecKey == null)
            {
                return null;
            }

            PgpPrivateKey privateKey = pgpSecKey.ExtractPrivateKey(passphrase);
            return privateKey;
        }

        // Decrypt the file
        private static void DecryptFile(string inputFilePath, string outputFilePath, Stream privateKeyStream, char[] passphrase)
        {
            using (Stream inputStream = PgpUtilities.GetDecoderStream(File.OpenRead(inputFilePath)))
            {
                PgpObjectFactory pgpF = new PgpObjectFactory(inputStream);
                PgpEncryptedDataList enc = null;
                PgpObject o = pgpF.NextPgpObject();

                // The first object might be a PGP marker packet
                if (o is PgpEncryptedDataList)
                {
                    enc = (PgpEncryptedDataList)o;
                }
                else
                {
                    enc = (PgpEncryptedDataList)pgpF.NextPgpObject();
                }

                // Get the encrypted data object
                PgpPublicKeyEncryptedData pbe = null;
                foreach (PgpPublicKeyEncryptedData pked in enc.GetEncryptedDataObjects())
                {
                    pbe = pked;
                    break;
                }

                if (pbe == null)
                {
                    throw new ArgumentException("Failed to find encrypted data in the PGP file.");
                }

                // Load the private key
                PgpSecretKeyRingBundle pgpSec = new PgpSecretKeyRingBundle(PgpUtilities.GetDecoderStream(privateKeyStream));
                PgpPrivateKey sKey = FindSecretKey(pgpSec, pbe.KeyId, passphrase);

                if (sKey == null)
                {
                    throw new ArgumentException("Failed to find private key.");
                }

                // Decrypt the data stream
                Stream clear = pbe.GetDataStream(sKey);
                PgpObjectFactory plainFact = new PgpObjectFactory(clear);

                PgpObject message = plainFact.NextPgpObject();

                if (message is PgpCompressedData)
                {
                    PgpCompressedData cData = (PgpCompressedData)message;
                    PgpObjectFactory pgpFact = new PgpObjectFactory(cData.GetDataStream());

                    message = pgpFact.NextPgpObject();
                }

                if (message is PgpLiteralData)
                {
                    PgpLiteralData ld = (PgpLiteralData)message;
                    Stream unc = ld.GetInputStream();
                    using (Stream output = File.Create(outputFilePath))
                    {
                        Streams.PipeAll(unc, output);
                    }
                }
                else
                {
                    throw new ArgumentException("Message is not a simple encrypted file.");
                }

                if (pbe.IsIntegrityProtected() && !pbe.Verify())
                {
                    Console.Error.WriteLine("Message failed integrity check.");
                }
                else
                {
                    Console.WriteLine("File decrypted successfully.");
                }
            }
        }


        public static void EncryptFile(string inputFilePath, string outputFilePath, string publicKey)
        {
            // Convert the public key string into a byte array
            byte[] publicKeyBytes = Encoding.ASCII.GetBytes(publicKey);

            // Create a memory stream to read the public key data
            using (MemoryStream inputStream = new MemoryStream(publicKeyBytes))
            {
                try
                {
                    // Use BouncyCastle PGP object factory to read the public key
                    PgpPublicKeyRingBundle publicKeyRingBundle = new PgpPublicKeyRingBundle(PgpUtilities.GetDecoderStream(inputStream));

                    // Iterate through the keys in the bundle and print details
                    foreach (PgpPublicKeyRing keyRing in publicKeyRingBundle.GetKeyRings())
                    {
                        foreach (PgpPublicKey key in keyRing.GetPublicKeys())
                        {
                            if (key.IsEncryptionKey)
                            {
                                EncryptFile(inputFilePath, outputFilePath, key);
                                return;
                            }
                                
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading PGP public key: {ex.Message}");
                }
            }

            // Encrypt the file
            
        }

        public static void DecryptFile(string encryptedFilePath, string outputFilePath, string privateKey, string passphrase)
        {
            using (Stream privateKeyStream = GenerateStreamFromString(privateKey))
            {
                DecryptFile(encryptedFilePath, outputFilePath, privateKeyStream, passphrase.ToCharArray());
            }
        }

        private static Stream GenerateStreamFromString(string s)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            writer.Write(s);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }
    }

}
