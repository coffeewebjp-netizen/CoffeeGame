using NUnit.Framework;

namespace CoffeeGame.Integration.Tests
{
    public sealed class CoffeeGameDeepLinkTests
    {
        [Test]
        public void AppCallback_ParsesQueryBearerAndState()
        {
            const string token = "cgt_id.secret";
            const string state = "abc123abc123abc123abc123abc123ab";
            string url = CoffeeGameDeepLink.AppCallback + "?state=" + state + "&bearer=" + token;

            Assert.That(CoffeeGameDeepLink.TryParseCallback(url, state, out string parsed, out string error), Is.True, error);
            Assert.That(parsed, Is.EqualTo(token));
        }

        [Test]
        public void AppCallback_RejectsMismatchedState()
        {
            string url = CoffeeGameDeepLink.AppCallback + "?state=nope&bearer=cgt_id.secret";
            Assert.That(CoffeeGameDeepLink.TryParseCallback(url, "expected-state-value-0123456789ab", out _, out _), Is.False);
        }
    }
}
