using Dcs.Ipia.Application;
using Dcs.Ipia.Tests.Fakes;
using Dcs.Ipia.Dcs;
using Dcs.Ipia.Domain;
using Dcs.Ipia.Infrastructure;
using Dcs.Ipia.Packaging;
using Dcs.Ipia.Rendering;
using Microsoft.Data.Sqlite;
using Xunit;
using Dcs.Ipia.Application.Interfaces;

namespace Dcs.Ipia.Tests
{
    /// <summary>Represents application tests.</summary>
    public sealed class ApplicationTests
    {
        #region BEGIN Public
        /// <summary>service exposes crud.</summary>
        /// <returns>A task that completes when the operation finishes</returns>
        [Fact]
        public async Task Service_exposes_crud()
        {
            var repo = new MemoryRepository();
            var service = new ModuleService(repo);
            var generated = await service.SaveAsync(new(null, "F-14", "Tomcat"));
            Assert.NotEqual(Guid.Empty, generated.Id);
            var id = Guid.NewGuid();
            var saved = await service.SaveAsync(new(id, "UH-1H", "Huey"));
            Assert.Equal(id, saved.Id);
            Assert.Equal(saved, await service.GetAsync(saved.Id));
            Assert.Equal(2, (await service.ListAsync()).Count);
            Assert.True(await service.DeleteAsync(saved.Id));
            Assert.Null(await service.GetAsync(saved.Id));
            Assert.False(await service.DeleteAsync(saved.Id));
        }
        #endregion END Public
    }
}
