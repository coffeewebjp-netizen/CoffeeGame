using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System.Runtime.InteropServices;
#endif

namespace CoffeeGame.Integration
{
    public interface ICoffeeGameAccessTokenProvider
    {
        bool HasAccessToken { get; }
        Task<string> LoadAccessTokenAsync(CancellationToken cancellationToken = default);
    }

    public interface ICoffeeGameAccessTokenStore : ICoffeeGameAccessTokenProvider
    {
        Task SaveAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default);
        Task DeleteAccessTokenAsync(CancellationToken cancellationToken = default);
    }

    public static class CoffeeGameAccessToken
    {
        private const string BearerPrefix = "Bearer ";
        private const string TokenPrefix = "cgt_";

        public static string Normalize(string value)
        {
            var token = value?.Trim();
            if (token != null && token.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
            {
                token = token.Substring(BearerPrefix.Length).Trim();
            }

            if (!IsValid(token))
            {
                throw new ArgumentException("CoffeeLearning returned an invalid CoffeeGAME credential.", nameof(value));
            }

            return token;
        }

        public static bool IsValid(string value)
        {
            if (string.IsNullOrWhiteSpace(value)
                || !value.StartsWith(TokenPrefix, StringComparison.Ordinal)
                || value.Length > 4096)
            {
                return false;
            }

            foreach (var character in value)
            {
                if (char.IsWhiteSpace(character) || char.IsControl(character))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Extracts the account hint embedded by the CoffeeLearning cgt token format.
        /// This is display-only metadata. The server remains authoritative for authentication.
        /// </summary>
        public static bool TryGetAccountEmail(string value, out string email)
        {
            email = string.Empty;
            string token;
            try
            {
                token = Normalize(value);
            }
            catch
            {
                return false;
            }

            var parts = token.Substring(TokenPrefix.Length).Split('.');
            if (parts.Length != 3 || string.IsNullOrEmpty(parts[0]))
            {
                return false;
            }

            try
            {
                string encoded = parts[0].Replace('-', '+').Replace('_', '/');
                int remainder = encoded.Length % 4;
                if (remainder == 1)
                {
                    return false;
                }
                if (remainder > 0)
                {
                    encoded = encoded.PadRight(encoded.Length + 4 - remainder, '=');
                }

                string candidate = Encoding.UTF8.GetString(Convert.FromBase64String(encoded))
                    .Trim()
                    .ToLowerInvariant();
                if (!IsSafeEmail(candidate))
                {
                    return false;
                }

                email = candidate;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsSafeEmail(string value)
        {
            int at = value.IndexOf('@');
            if (value.Length < 3 || value.Length > 254 || at < 1 || at == value.Length - 1)
            {
                return false;
            }

            foreach (char character in value)
            {
                if (char.IsWhiteSpace(character) || char.IsControl(character)
                    || character == '<' || character == '>')
                {
                    return false;
                }
            }

            return true;
        }
    }

    public sealed class CoffeeGameCredentialException : Exception
    {
        public CoffeeGameCredentialException(string message, int nativeErrorCode = 0)
            : base(message)
        {
            NativeErrorCode = nativeErrorCode;
        }

        public int NativeErrorCode { get; }
    }

    public static class CoffeeGameAccessTokenStoreFactory
    {
        public static ICoffeeGameAccessTokenStore CreatePlatformDefault()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            var path = Path.Combine(
                Application.persistentDataPath,
                "CoffeeLearning",
                "coffee-game-token.v1.dpapi");
            return new WindowsDpapiAccessTokenStore(path);
#else
            return new UnsupportedCoffeeGameAccessTokenStore();
#endif
        }
    }

    public sealed class UnsupportedCoffeeGameAccessTokenStore : ICoffeeGameAccessTokenStore
    {
        public bool HasAccessToken => false;

        public Task<string> LoadAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new PlatformNotSupportedException(
                "Secure CoffeeLearning credential storage is not configured for this platform.");
        }

        public Task SaveAccessTokenAsync(
            string accessToken,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new PlatformNotSupportedException(
                "Secure CoffeeLearning credential storage is not configured for this platform.");
        }

        public Task DeleteAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    /// <summary>
    /// Windows/Steam credential store protected for the current Windows user with DPAPI.
    /// The encrypted file is replaced atomically and contains no plaintext fallback.
    /// </summary>
    public sealed class WindowsDpapiAccessTokenStore : ICoffeeGameAccessTokenStore
    {
        private const int CryptProtectUiForbidden = 0x1;
        private const int MoveFileReplaceExisting = 0x1;
        private const int MoveFileWriteThrough = 0x8;
        private static readonly byte[] OptionalEntropy = Encoding.UTF8.GetBytes(
            "CoffeeGAME/CoffeeLearning/cgt/v1");

        private readonly object gate = new object();
        private readonly string filePath;

        public WindowsDpapiAccessTokenStore(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Credential file path is required.", nameof(filePath));
            }

            this.filePath = Path.GetFullPath(filePath);
        }

        public bool HasAccessToken
        {
            get
            {
                lock (gate)
                {
                    return File.Exists(filePath);
                }
            }
        }

        public Task<string> LoadAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!File.Exists(filePath))
                {
                    throw new CoffeeGameCredentialException(
                        "The CoffeeLearning credential has not been saved.");
                }

                byte[] encrypted = null;
                byte[] plaintext = null;
                try
                {
                    encrypted = File.ReadAllBytes(filePath);
                    plaintext = UnprotectForCurrentUser(encrypted);
                    var token = Encoding.UTF8.GetString(plaintext);
                    return Task.FromResult(CoffeeGameAccessToken.Normalize(token));
                }
                catch (CoffeeGameCredentialException)
                {
                    throw;
                }
                catch
                {
                    throw new CoffeeGameCredentialException(
                        "The CoffeeLearning credential could not be read.");
                }
                finally
                {
                    Clear(encrypted);
                    Clear(plaintext);
                }
            }
        }

        public Task SaveAccessTokenAsync(
            string accessToken,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = CoffeeGameAccessToken.Normalize(accessToken);
            lock (gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var directory = Path.GetDirectoryName(filePath);
                if (string.IsNullOrWhiteSpace(directory))
                {
                    throw new CoffeeGameCredentialException(
                        "The CoffeeLearning credential location is invalid.");
                }

                byte[] plaintext = null;
                byte[] encrypted = null;
                string temporaryPath = null;
                try
                {
                    plaintext = Encoding.UTF8.GetBytes(normalized);
                    encrypted = ProtectForCurrentUser(plaintext);
                    Directory.CreateDirectory(directory);
                    temporaryPath = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";

                    using (var stream = new FileStream(
                        temporaryPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        4096,
                        FileOptions.WriteThrough))
                    {
                        stream.Write(encrypted, 0, encrypted.Length);
                        stream.Flush(true);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    if (File.Exists(filePath))
                    {
                        ReplaceExistingFile(temporaryPath, filePath);
                    }
                    else
                    {
                        File.Move(temporaryPath, filePath);
                    }

                    temporaryPath = null;
                    return Task.CompletedTask;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (CoffeeGameCredentialException)
                {
                    throw;
                }
                catch
                {
                    throw new CoffeeGameCredentialException(
                        "The CoffeeLearning credential could not be saved.");
                }
                finally
                {
                    if (!string.IsNullOrEmpty(temporaryPath))
                    {
                        try
                        {
                            File.Delete(temporaryPath);
                        }
                        catch
                        {
                            // A failed cleanup never justifies exposing credential material.
                        }
                    }

                    Clear(plaintext);
                    Clear(encrypted);
                }
            }
        }

        public Task DeleteAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (gate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }

                    return Task.CompletedTask;
                }
                catch
                {
                    throw new CoffeeGameCredentialException(
                        "The CoffeeLearning credential could not be deleted.");
                }
            }
        }

        private static byte[] ProtectForCurrentUser(byte[] plaintext)
        {
            return TransformWithDpapi(plaintext, true);
        }

        private static void ReplaceExistingFile(string replacementPath, string destinationPath)
        {
            if (!MoveFileEx(
                replacementPath,
                destinationPath,
                MoveFileReplaceExisting | MoveFileWriteThrough))
            {
                throw new CoffeeGameCredentialException(
                    "Windows could not atomically replace the CoffeeLearning credential.",
                    Marshal.GetLastWin32Error());
            }
        }

        private static byte[] UnprotectForCurrentUser(byte[] encrypted)
        {
            return TransformWithDpapi(encrypted, false);
        }

        private static byte[] TransformWithDpapi(byte[] input, bool protect)
        {
            if (input == null || input.Length == 0)
            {
                throw new CoffeeGameCredentialException(
                    "The CoffeeLearning credential data is invalid.");
            }

            var inputBlob = CreateBlob(input);
            var entropyBlob = CreateBlob(OptionalEntropy);
            var outputBlob = default(DataBlob);
            try
            {
                var succeeded = protect
                    ? CryptProtectData(
                        ref inputBlob,
                        null,
                        ref entropyBlob,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        CryptProtectUiForbidden,
                        out outputBlob)
                    : CryptUnprotectData(
                        ref inputBlob,
                        IntPtr.Zero,
                        ref entropyBlob,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        CryptProtectUiForbidden,
                        out outputBlob);
                if (!succeeded || outputBlob.Data == IntPtr.Zero || outputBlob.Size <= 0)
                {
                    throw new CoffeeGameCredentialException(
                        "Windows could not protect the CoffeeLearning credential.");
                }

                var result = new byte[outputBlob.Size];
                Marshal.Copy(outputBlob.Data, result, 0, result.Length);
                return result;
            }
            finally
            {
                FreeBlob(ref inputBlob, false);
                FreeBlob(ref entropyBlob, false);
                FreeBlob(ref outputBlob, true);
            }
        }

        private static DataBlob CreateBlob(byte[] data)
        {
            var blob = new DataBlob
            {
                Size = data.Length,
                Data = Marshal.AllocHGlobal(data.Length)
            };
            Marshal.Copy(data, 0, blob.Data, data.Length);
            return blob;
        }

        private static void FreeBlob(ref DataBlob blob, bool localAlloc)
        {
            if (blob.Data == IntPtr.Zero)
            {
                return;
            }

            if (localAlloc)
            {
                LocalFree(blob.Data);
            }
            else
            {
                Marshal.FreeHGlobal(blob.Data);
            }

            blob.Data = IntPtr.Zero;
            blob.Size = 0;
        }

        private static void Clear(byte[] value)
        {
            if (value != null)
            {
                Array.Clear(value, 0, value.Length);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DataBlob
        {
            public int Size;
            public IntPtr Data;
        }

        [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptProtectData(
            ref DataBlob dataIn,
            string description,
            ref DataBlob optionalEntropy,
            IntPtr reserved,
            IntPtr prompt,
            int flags,
            out DataBlob dataOut);

        [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CryptUnprotectData(
            ref DataBlob dataIn,
            IntPtr description,
            ref DataBlob optionalEntropy,
            IntPtr reserved,
            IntPtr prompt,
            int flags,
            out DataBlob dataOut);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LocalFree(IntPtr memory);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "MoveFileExW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool MoveFileEx(
            string existingFileName,
            string newFileName,
            int flags);
    }
#endif
}
