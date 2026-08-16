using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace CoffeeGame.Integration.Tests
{
    public sealed class CoffeeGameAccessTokenStoreTests
    {
        [TestCase("cgt_id.secret", "cgt_id.secret")]
        [TestCase("Bearer cgt_id.secret", "cgt_id.secret")]
        public void NormalizeAcceptsRawOrBearerAndReturnsCanonicalToken(string value, string expected)
        {
            Assert.That(CoffeeGameAccessToken.Normalize(value), Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Bearer movie_token")]
        [TestCase("cgt_bad token")]
        public void NormalizeRejectsInvalidCredentialWithoutEchoingIt(string value)
        {
            var exception = Assert.Throws<ArgumentException>(() => CoffeeGameAccessToken.Normalize(value));
            if (!string.IsNullOrEmpty(value))
            {
                Assert.That(exception.Message, Does.Not.Contain(value));
            }
        }

        [Test]
        public void AccountHintDecodesOnlyTheDedicatedTokenSubject()
        {
            const string token =
                "cgt_cGxheWVyQGV4YW1wbGUuY29t.cgtok_test.abcdefghijklmnopqrstuvwxyz0123456789";

            Assert.That(CoffeeGameAccessToken.TryGetAccountEmail(token, out string email), Is.True);
            Assert.That(email, Is.EqualTo("player@example.com"));
            Assert.That(
                CoffeeGameAccessToken.TryGetAccountEmail("cgt_not-base64.id.secret", out _),
                Is.False);
        }

        [Test]
        public void UnsupportedStoreFailsClosed()
        {
            var store = new UnsupportedCoffeeGameAccessTokenStore();

            Assert.That(store.HasAccessToken, Is.False);
            Assert.ThrowsAsync<PlatformNotSupportedException>(async () =>
                await store.LoadAccessTokenAsync());
            Assert.ThrowsAsync<PlatformNotSupportedException>(async () =>
                await store.SaveAccessTokenAsync("cgt_id.secret"));
        }

        [Test]
        public void StoreOperationsObservePreCancellation()
        {
            var store = new UnsupportedCoffeeGameAccessTokenStore();
            var cancellation = new CancellationToken(true);

            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await store.LoadAccessTokenAsync(cancellation));
            Assert.ThrowsAsync<TaskCanceledException>(async () =>
                await store.DeleteAccessTokenAsync(cancellation));
        }

#if UNITY_EDITOR_WIN
        [Test]
        public async Task WindowsStoreEncryptsOverwritesLoadsAndDeletesForCurrentUser()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "CoffeeGameLearningTests",
                Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "token.dpapi");
            var store = new WindowsDpapiAccessTokenStore(path);
            const string firstToken = "cgt_first-id.first-secret";
            const string secondToken = "cgt_second-id.second-secret";

            try
            {
                Assert.That(store.HasAccessToken, Is.False);
                await store.SaveAccessTokenAsync("Bearer " + firstToken);

                Assert.That(store.HasAccessToken, Is.True);
                Assert.That(await store.LoadAccessTokenAsync(), Is.EqualTo(firstToken));
                Assert.That(
                    Encoding.UTF8.GetString(File.ReadAllBytes(path)),
                    Does.Not.Contain(firstToken));

                await store.SaveAccessTokenAsync(secondToken);
                Assert.That(await store.LoadAccessTokenAsync(), Is.EqualTo(secondToken));
                Assert.That(
                    Directory.GetFiles(directory, "*.tmp", SearchOption.TopDirectoryOnly),
                    Is.Empty);

                await store.DeleteAccessTokenAsync();
                Assert.That(store.HasAccessToken, Is.False);
                await store.DeleteAccessTokenAsync();
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public async Task WindowsStore_AtomicReplaceFailureKeepsPreviousTokenAndReportsOnlyCode()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "CoffeeGameLearningTests",
                Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "token.dpapi");
            var store = new WindowsDpapiAccessTokenStore(path);
            const string originalToken = "cgt_original-id.original-secret";
            const string replacementToken = "cgt_replacement-id.replacement-secret";

            try
            {
                await store.SaveAccessTokenAsync(originalToken);
                CoffeeGameCredentialException exception;
                using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    exception = Assert.ThrowsAsync<CoffeeGameCredentialException>(async () =>
                        await store.SaveAccessTokenAsync(replacementToken));
                }

                Assert.That(exception.NativeErrorCode, Is.GreaterThan(0));
                Assert.That(exception.Message, Does.Not.Contain(originalToken));
                Assert.That(exception.Message, Does.Not.Contain(replacementToken));
                Assert.That(exception.Message, Does.Not.Contain(path));
                Assert.That(await store.LoadAccessTokenAsync(), Is.EqualTo(originalToken));
                Assert.That(
                    Directory.GetFiles(directory, "*.tmp", SearchOption.TopDirectoryOnly),
                    Is.Empty);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
        }

        [Test]
        public void WindowsStoreRejectsCorruptCiphertextWithSafeError()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "CoffeeGameLearningTests",
                Guid.NewGuid().ToString("N"));
            var path = Path.Combine(directory, "token.dpapi");
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(path, Encoding.UTF8.GetBytes("cgt_plaintext_must_not_load"));
            var store = new WindowsDpapiAccessTokenStore(path);

            try
            {
                var exception = Assert.ThrowsAsync<CoffeeGameCredentialException>(async () =>
                    await store.LoadAccessTokenAsync());
                Assert.That(exception.Message, Does.Not.Contain("cgt_plaintext_must_not_load"));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }
#endif
    }
}
