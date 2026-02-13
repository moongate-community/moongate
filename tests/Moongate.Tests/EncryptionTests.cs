using System.Text;
using Moongate.UO.Data.Version;
using Moongate.UO.Encryption;

namespace Moongate.Tests;

[TestFixture]
public class ClientEncryptorTests
{
    private ClientVersion testVersion;
    private ClientKeys testKeys;
    private uint testSeed;

    [Test]
    public void CalculateKeys_ShouldProduceConsistentResults()
    {
        // Arrange
        var version = new ClientVersion(1, 0, 0, 0);

        // Act
        var keys1 = ClientEncryptor.CalculateKeys(version);
        var keys2 = ClientEncryptor.CalculateKeys(version);

        // Assert
        Assert.That(keys1.Key1, Is.EqualTo(keys2.Key1));
        Assert.That(keys1.Key2, Is.EqualTo(keys2.Key2));
    }

    [Test]
    public void CalculateKeys_WithSpecificValues_ShouldProduceExpectedResults()
    {
        // Arrange

        var version = new ClientVersion(1, 2, 3, 4);

        // Act
        var keys = ClientEncryptor.CalculateKeys(version);

        // Assert
        Assert.That(keys.Key1, Is.Not.EqualTo(0));
        Assert.That(keys.Key2, Is.Not.EqualTo(0));
        Assert.That(keys.Key1, Is.Not.EqualTo(keys.Key2));

        // Print keys for debugging if needed
        TestContext.Out.WriteLine($"Key1: 0x{keys.Key1:X8} ({keys.Key1})");
        TestContext.Out.WriteLine($"Key2: 0x{keys.Key2:X8} ({keys.Key2})");
    }

    [Test]
    public void Decrypt_ShouldNotModifyOriginalData()
    {
        // Arrange
        byte[] testData = { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var encryptedData = ClientEncryptor.Encrypt((byte[])testData.Clone(), testKeys, testSeed);
        var encryptedCopy = (byte[])encryptedData.Clone();

        // Act
        var decrypted = ClientEncryptor.Decrypt(encryptedData, testKeys, testSeed);

        // Assert
        Assert.That(encryptedData, Is.EqualTo(encryptedCopy));
        Assert.That(decrypted, Is.EqualTo(testData));
    }

    [Test]
    public void Encrypt_ShouldNotModifyOriginalData()
    {
        // Arrange
        byte[] originalData = { 0x01, 0x02, 0x03, 0x04, 0x05 };
        var originalCopy = (byte[])originalData.Clone();

        // Act
        var encrypted = ClientEncryptor.Encrypt(originalData, testKeys, testSeed);

        // Assert
        Assert.That(originalData, Is.EqualTo(originalCopy));
        Assert.That(encrypted, Is.Not.EqualTo(originalData));
    }

    [Test]
    public void Encrypt_WithDifferentKeys_ShouldProduceDifferentResults()
    {
        // Arrange
        var testData = Encoding.UTF8.GetBytes("Same data, different keys");

        // var version1 = new Version { Major = 1, Minor = 0, Revision = 0, Patch = 0 };
        //var version2 = new Version { Major = 2, Minor = 0, Revision = 0, Patch = 0 };

        var version1 = new ClientVersion(1, 0, 0, 0);
        var version2 = new ClientVersion(2, 0, 0, 0);
        var keys1 = ClientEncryptor.CalculateKeys(version1);
        var keys2 = ClientEncryptor.CalculateKeys(version2);

        // Act
        var encrypted1 = ClientEncryptor.Encrypt((byte[])testData.Clone(), keys1, testSeed);
        var encrypted2 = ClientEncryptor.Encrypt((byte[])testData.Clone(), keys2, testSeed);

        // Assert
        Assert.That(encrypted1, Is.Not.EqualTo(encrypted2));
    }

    [Test]
    public void Encrypt_WithDifferentSeeds_ShouldProduceDifferentResults()
    {
        // Arrange
        var testData = Encoding.UTF8.GetBytes("Same data, different seeds");
        uint seed1 = 111;
        uint seed2 = 222;

        // Act
        var encrypted1 = ClientEncryptor.Encrypt((byte[])testData.Clone(), testKeys, seed1);
        var encrypted2 = ClientEncryptor.Encrypt((byte[])testData.Clone(), testKeys, seed2);

        // Assert
        Assert.That(encrypted1, Is.Not.EqualTo(encrypted2));
    }

    [Test]
    public void EncryptDecrypt_MultipleCycles_ShouldWork()
    {
        // Arrange
        var originalText = "Multiple encryption cycles test";
        var data = Encoding.UTF8.GetBytes(originalText);

        // Act & Assert - Multiple encrypt/decrypt cycles
        for (var i = 0; i < 10; i++)
        {
            data = ClientEncryptor.Encrypt(data, testKeys, testSeed);
            data = ClientEncryptor.Decrypt(data, testKeys, testSeed);
        }

        var finalText = Encoding.UTF8.GetString(data);
        Assert.That(finalText, Is.EqualTo(originalText));
    }

    [Test]
    public void EncryptDecrypt_ShouldReturnOriginalData()
    {
        // Arrange
        var originalText = "Hello World! This is a test message.";
        var originalData = Encoding.UTF8.GetBytes(originalText);

        // Act
        var encryptedData = ClientEncryptor.Encrypt(originalData, testKeys, testSeed);
        var decryptedData = ClientEncryptor.Decrypt(encryptedData, testKeys, testSeed);
        var decryptedText = Encoding.UTF8.GetString(decryptedData);

        // Assert
        Assert.That(decryptedText, Is.EqualTo(originalText));
        Assert.That(decryptedData, Is.EqualTo(originalData));
    }

    [Test, TestCase(1u), TestCase(100u), TestCase(12345u), TestCase(0xFFFFFFFFu)]
    public void EncryptDecrypt_WithDifferentSeeds_ShouldWork(uint seed)
    {
        // Arrange
        byte[] testData = { 0x01, 0x02, 0x03, 0x04, 0x05 };

        // Act
        var encrypted = ClientEncryptor.Encrypt((byte[])testData.Clone(), testKeys, seed);
        var decrypted = ClientEncryptor.Decrypt(encrypted, testKeys, seed);

        // Assert
        Assert.That(decrypted, Is.EqualTo(testData));
    }

    [Test, TestCase(1, 0, 0, 0), TestCase(2, 5, 10, 15), TestCase(255, 255, 255, 255)]
    public void EncryptDecrypt_WithDifferentVersions_ShouldWork(int major, int minor, int revision, int patch)
    {
        // Arrange
        var testData = Encoding.UTF8.GetBytes("Test data");
        var version = new ClientVersion(major, minor, revision, patch);

        // Act
        var keys = ClientEncryptor.CalculateKeys(version);
        var encrypted = ClientEncryptor.Encrypt((byte[])testData.Clone(), keys, testSeed);
        var decrypted = ClientEncryptor.Decrypt(encrypted, keys, testSeed);

        // Assert
        Assert.That(decrypted, Is.EqualTo(testData));
    }

    [Test]
    public void EncryptDecrypt_WithEmptyData_ShouldWork()
    {
        // Arrange
        var emptyData = new byte[0];

        // Act
        var encrypted = ClientEncryptor.Encrypt(emptyData, testKeys, testSeed);
        var decrypted = ClientEncryptor.Decrypt(encrypted, testKeys, testSeed);

        // Assert
        Assert.That(decrypted, Is.EqualTo(emptyData));
        Assert.That(encrypted.Length, Is.EqualTo(0));
    }

    [Test]
    public void EncryptDecrypt_WithLargeData_ShouldWork()
    {
        // Arrange
        var largeData = new byte[1000];

        for (var i = 0; i < largeData.Length; i++)
        {
            largeData[i] = (byte)(i % 256);
        }

        // Act
        var encrypted = ClientEncryptor.Encrypt((byte[])largeData.Clone(), testKeys, testSeed);
        var decrypted = ClientEncryptor.Decrypt(encrypted, testKeys, testSeed);

        // Assert
        Assert.That(decrypted, Is.EqualTo(largeData));
    }

    [Test]
    public void EncryptDecrypt_WithSingleByte_ShouldWork()
    {
        // Arrange
        byte[] singleByte = { 0xAB };

        // Act
        var encrypted = ClientEncryptor.Encrypt((byte[])singleByte.Clone(), testKeys, testSeed);
        var decrypted = ClientEncryptor.Decrypt(encrypted, testKeys, testSeed);

        // Assert
        Assert.That(decrypted, Is.EqualTo(singleByte));
        Assert.That(encrypted.Length, Is.EqualTo(1));
    }

    [SetUp]
    public void SetUp()
    {
        testVersion = new(1, 2, 3, 4);
        testKeys = ClientEncryptor.CalculateKeys(testVersion);
        testSeed = 12345;
    }
}
