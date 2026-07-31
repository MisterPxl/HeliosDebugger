using System;
using System.Security.Cryptography;

namespace HeliosDebugger
{
    public enum HeliosAccessOperation
    {
        OpenDebugger,
        ExecutePinnedItem,
        SubmitBugReport,
        ViewSensitiveSystemInfo
    }

    public enum HeliosAccessDecision
    {
        Allow,
        Deny,
        Challenge
    }

    public readonly struct HeliosAccessRequest
    {
        public HeliosAccessRequest(HeliosAccessOperation operation, string resourceId = null)
        {
            Operation = operation;
            ResourceId = resourceId ?? string.Empty;
        }

        public HeliosAccessOperation Operation { get; }
        public string ResourceId { get; }
    }

    public interface IHeliosAccessPolicy
    {
        HeliosAccessDecision Evaluate(HeliosAccessRequest request);
    }

    public interface IHeliosChallengeAccessPolicy : IHeliosAccessPolicy
    {
        bool TryUnlock(string credential);
        void Lock();
    }

    public sealed class HeliosAllowAllAccessPolicy : IHeliosAccessPolicy
    {
        public HeliosAccessDecision Evaluate(HeliosAccessRequest request)
        {
            return HeliosAccessDecision.Allow;
        }
    }

    public sealed class HeliosDenyAllAccessPolicy : IHeliosAccessPolicy
    {
        public HeliosAccessDecision Evaluate(HeliosAccessRequest request)
        {
            return HeliosAccessDecision.Deny;
        }
    }

    public sealed class HeliosPinAccessPolicy : IHeliosChallengeAccessPolicy
    {
        public const int SaltBytes = 16;
        public const int HashBytes = 32;
        public const int Iterations = 100000;

        private readonly byte[] _salt;
        private readonly byte[] _expectedHash;
        private readonly TimeSpan _sessionDuration;
        private DateTime _unlockedUntilUtc;

        public HeliosPinAccessPolicy(string saltBase64, string hashBase64, TimeSpan sessionDuration)
        {
            _salt = Decode(saltBase64, nameof(saltBase64));
            _expectedHash = Decode(hashBase64, nameof(hashBase64));
            _sessionDuration = sessionDuration <= TimeSpan.Zero ? TimeSpan.FromMinutes(15d) : sessionDuration;
        }

        public bool IsUnlocked => DateTime.UtcNow < _unlockedUntilUtc;

        public HeliosAccessDecision Evaluate(HeliosAccessRequest request)
        {
            return IsUnlocked ? HeliosAccessDecision.Allow : HeliosAccessDecision.Challenge;
        }

        public bool TryUnlock(string credential)
        {
            if (string.IsNullOrEmpty(credential))
                return false;

            byte[] candidate = DeriveHash(credential, _salt);
            bool valid = FixedTimeEquals(candidate, _expectedHash);
            if (valid)
                _unlockedUntilUtc = DateTime.UtcNow.Add(_sessionDuration);
            return valid;
        }

        public void Lock()
        {
            _unlockedUntilUtc = DateTime.MinValue;
        }

        public static void CreateCredentials(string pin, out string saltBase64, out string hashBase64)
        {
            if (string.IsNullOrEmpty(pin))
                throw new ArgumentException("A PIN cannot be empty.", nameof(pin));

            byte[] salt = new byte[SaltBytes];
            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
                random.GetBytes(salt);

            byte[] hash = DeriveHash(pin, salt);
            saltBase64 = Convert.ToBase64String(salt);
            hashBase64 = Convert.ToBase64String(hash);
        }

        private static byte[] DeriveHash(string pin, byte[] salt)
        {
            using (Rfc2898DeriveBytes derive = new Rfc2898DeriveBytes(
                       pin,
                       salt,
                       Iterations,
                       HashAlgorithmName.SHA256))
            {
                return derive.GetBytes(HashBytes);
            }
        }

        private static byte[] Decode(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("PIN credentials are not configured.", parameterName);

            try
            {
                return Convert.FromBase64String(value);
            }
            catch (FormatException exception)
            {
                throw new ArgumentException("PIN credentials are invalid.", parameterName, exception);
            }
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
                return false;

            int difference = 0;
            for (int i = 0; i < left.Length; i++)
                difference |= left[i] ^ right[i];
            return difference == 0;
        }
    }

    public sealed class HeliosAccessController
    {
        private IHeliosAccessPolicy _policy;
        private Action _pendingContinuation;

        public HeliosAccessController(IHeliosAccessPolicy policy)
        {
            _policy = policy ?? new HeliosAllowAllAccessPolicy();
        }

        public event Action<HeliosAccessRequest> ChallengeRequested;

        public IHeliosAccessPolicy Policy => _policy;

        public void SetPolicy(IHeliosAccessPolicy policy)
        {
            _pendingContinuation = null;
            _policy = policy ?? new HeliosAllowAllAccessPolicy();
        }

        public HeliosAccessDecision Check(HeliosAccessRequest request)
        {
            HeliosAccessDecision decision = _policy.Evaluate(request);
            if (decision == HeliosAccessDecision.Challenge)
                ChallengeRequested?.Invoke(request);
            return decision;
        }

        public HeliosAccessDecision Request(HeliosAccessRequest request, Action continuation)
        {
            HeliosAccessDecision decision = _policy.Evaluate(request);
            if (decision == HeliosAccessDecision.Allow)
            {
                continuation?.Invoke();
            }
            else if (decision == HeliosAccessDecision.Challenge)
            {
                _pendingContinuation = continuation;
                ChallengeRequested?.Invoke(request);
            }
            return decision;
        }

        public bool TryUnlock(string credential)
        {
            IHeliosChallengeAccessPolicy challenge = _policy as IHeliosChallengeAccessPolicy;
            if (challenge == null || !challenge.TryUnlock(credential))
                return false;

            Action continuation = _pendingContinuation;
            _pendingContinuation = null;
            continuation?.Invoke();
            return true;
        }

        public void Lock()
        {
            _pendingContinuation = null;
            IHeliosChallengeAccessPolicy challenge = _policy as IHeliosChallengeAccessPolicy;
            challenge?.Lock();
        }
    }
}
