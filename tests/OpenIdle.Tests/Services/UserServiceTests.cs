using System;
using System.Threading.Tasks;
using Backend.Database.Entities;
using Backend.Services;
using OpenIdle.Tests.Database;

namespace OpenIdle.Tests.Services;

[TestFixture]
public sealed class UserServiceTests : IDisposable
{
    private readonly TestGameDb _db = new();

    public void Dispose()
    {
        _db.Dispose();
    }

    [Test]
    public async Task GetOrCreateBySubjectAsync_ReturnsSameUserForSameSubject()
    {
        UserService service = new(_db.Factory, new SocketRegistryService());

        User first = await service.GetOrCreateBySubjectAsync("subject-1");
        User second = await service.GetOrCreateBySubjectAsync("subject-1");

        Assert.That(second.UserId, Is.EqualTo(first.UserId));
    }

    [Test]
    public async Task GetOrCreateBySubjectAsync_CreatesDistinctUsersForDistinctSubjects()
    {
        UserService service = new(_db.Factory, new SocketRegistryService());

        User first = await service.GetOrCreateBySubjectAsync("subject-1");
        User second = await service.GetOrCreateBySubjectAsync("subject-2");

        Assert.That(second.UserId, Is.Not.EqualTo(first.UserId));
    }

    [Test]
    public async Task GetTestUserAsync_UsesReservedSubjectAndNotARealAccount()
    {
        UserService service = new(_db.Factory, new SocketRegistryService());

        User testUser = await service.GetTestUserAsync();
        User real = await service.GetOrCreateBySubjectAsync("some-zitadel-subject");

        Assert.Multiple(() =>
        {
            Assert.That(testUser.Subject, Is.EqualTo(UserService.TestUserSubject));
            Assert.That(testUser.UserId, Is.Not.EqualTo(real.UserId));
        });
    }
}
