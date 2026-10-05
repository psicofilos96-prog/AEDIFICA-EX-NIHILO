using NUnit.Framework;

namespace Aedifica.Tests.EditMode
{
    public sealed class ProjectFoundationEditModeTests
    {
        [Test]
        public void CoreAssemblyIsAvailable()
        {
            Assert.That(Core.ProjectIdentity.Name, Is.EqualTo("Aedifica: Ex Nihilo"));
        }
    }
}
