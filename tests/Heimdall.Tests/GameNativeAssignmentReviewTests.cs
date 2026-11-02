using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.TestSupport;
using Heimdall.WarThunder;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Verifies adapter-native review is isolated and unchanged native edits do not publish false draft mutations.</summary>
    public sealed class GameNativeAssignmentReviewTests
    {
        #region BEGIN Public
        /// <summary>Review validates native syntax without changing the document, its export or notification count.</summary>
        [Fact]
        public void Review_and_commit_use_the_same_native_edit_without_mutating_during_review()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var session = Open(issuer.Policy); var before = JsonSerializer.Serialize(session.Snapshot);
            var notifications = 0; session.Changed += (_, _) => notifications++;
            var pitch = WarThunderBackendFixture.Binding(session.Snapshot.Profile, "pitch");
            var edit = new WarThunderNativeEdit(WarThunderEditKind.Inverse, pitch.Id, Inverted: true);
            var review = session.ReviewNativeEdit(edit);
            Assert.True(review.Valid); Assert.True(review.Changed); Assert.Empty(review.Message);
            Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot)); Assert.Equal(0, notifications);
            session.ApplyNativeEdit(edit);
            Assert.True(session.HasChanges); Assert.Equal(1, notifications);
            var staged = JsonSerializer.Serialize(session.Snapshot);
            review = session.ReviewNativeEdit(edit);
            Assert.True(review.Valid); Assert.False(review.Changed);
            session.ApplyNativeEdit(edit);
            Assert.Equal(1, notifications); Assert.Equal(staged, JsonSerializer.Serialize(session.Snapshot));
            session.Discard();
            Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot)); Assert.False(session.HasChanges);
        }
        /// <summary>Unsupported records and invalid literals are rejected as feedback without modifying draft state.</summary>
        [Fact]
        public void Invalid_native_review_leaves_the_original_document_intact()
        {
            using var issuer = new OwnerLicenseTestIssuer(); var session = Open(issuer.Policy);
            var before = JsonSerializer.Serialize(session.Snapshot);
            var review = session.ReviewNativeEdit(new WarThunderNativeEdit(WarThunderEditKind.Clear, "missing"));
            Assert.False(review.Valid); Assert.False(review.Changed); Assert.NotEmpty(review.Message);
            Assert.Equal(before, JsonSerializer.Serialize(session.Snapshot)); Assert.False(session.HasChanges);
            Assert.Throws<ArgumentNullException>(() => session.ReviewNativeEdit(null!));
        }
        /// <summary>Read-only edition policy applies to preview as well as mutation.</summary>
        [Fact]
        public void Read_only_sessions_reject_native_review()
        {
            var session = Open(new EditionPolicy());
            var pitch = WarThunderBackendFixture.Binding(session.Snapshot.Profile, "pitch");
            var review = session.ReviewNativeEdit(new WarThunderNativeEdit(WarThunderEditKind.Inverse, pitch.Id, Inverted: true));
            Assert.False(review.Valid); Assert.False(review.Changed); Assert.NotEmpty(review.Message); Assert.False(session.HasChanges);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static GameProfileSession Open(EditionPolicy policy)
        {
            var backend = new WarThunderProfileBackend();
            return new(backend, new(1, Guid.NewGuid(), "Review", WarThunderBackendFixture.Import(backend), new()), policy, [], () => []);
        }
        #endregion END private helpers
    }
}
