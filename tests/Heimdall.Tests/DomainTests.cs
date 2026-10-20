using Heimdall.Application;
using Heimdall.Tests.Fakes;
using Heimdall.Dcs;
using Heimdall.Domain;
using Heimdall.Infrastructure;
using Heimdall.Packaging;
using Heimdall.Rendering;
using Microsoft.Data.Sqlite;
using Xunit;
using Heimdall.Application.Interfaces;

namespace Heimdall.Tests
{
    /// <summary>Represents domain tests.</summary>
    public sealed class DomainTests
    {
        #region BEGIN Public
        /// <summary>valid module is normalized.</summary>
        [Fact]
        public void Valid_module_is_normalized() =>
            Assert.Equal("UH-1H", new DcsModule(Guid.NewGuid(), " UH-1H ", " Huey ").DcsId);
        /// <summary>empty id is rejected.</summary>
        [Fact]
        public void Empty_id_is_rejected() => Assert.Throws<ArgumentException>(() => new DcsModule(Guid.Empty, "UH-1H",
                                                                                                   "Huey"));
        /// <summary>missing names are rejected.</summary>
        /// <param name="id">The identifier.</param>
        /// <param name="name">The name.</param>
        [Theory]
        [InlineData("", "Huey")]
        [InlineData("UH-1H", " ")]
        public void Missing_names_are_rejected(string id, string name) =>
            Assert.Throws<ArgumentException>(() => new DcsModule(Guid.NewGuid(), id, name));
        #endregion END Public
    }
}
