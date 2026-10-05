using NUnit.Framework;
using UnityEngine.SceneManagement;

namespace Aedifica.Tests.PlayMode
{
    public sealed class ProjectFoundationPlayModeTests
    {
        [Test]
        public void ActiveSceneIsLoaded()
        {
            Assert.That(SceneManager.GetActiveScene().isLoaded, Is.True);
        }
    }
}
