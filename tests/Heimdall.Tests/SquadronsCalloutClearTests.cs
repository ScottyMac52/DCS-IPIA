using System.Text.Json;
using Heimdall.Application;
using Heimdall.Application.Games;
using Heimdall.Squadrons;
using Heimdall.TestSupport;
using Xunit;

namespace Heimdall.Tests
{
    /// <summary>Protects reviewed whole-callout clearing across native axis direction records.</summary>
    public sealed class SquadronsCalloutClearTests
    {
        private const string Positive = "IncomStarshipInputConcepts.ConceptThrottle.5";
        private const string Negative = "IncomStarshipInputConcepts.ConceptThrottle.4";
        #region BEGIN Public
        /// <summary>Reviewing leaves source unchanged; acceptance clears both directions once and discard restores exact source.</summary>
        [Fact]
        public void Axis_callout_clear_is_reviewed_and_staged_atomically()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var (session, backend, source) = Open(issuer.Policy);
            var original = backend.ReadBindings(session.Snapshot.Profile);
            var notifications = 0; session.Changed += (_, _) => notifications++;
            var review = session.PreviewClearCapturedRecords([Positive, Negative]);
            Assert.Contains(Positive, review.Summary, StringComparison.Ordinal);
            Assert.Contains(Negative, review.Summary, StringComparison.Ordinal);
            Assert.False(session.HasChanges);
            Assert.Equal(0, notifications);
            Assert.Equal(source, session.Export().Files[SquadronsProfileBackend.FileName]);
            session.ApplyControllerChange(review);
            Assert.Equal(1, notifications);
            var remaining = backend.ReadBindings(session.Snapshot.Profile);
            Assert.Equal(original.Count - 2, remaining.Count);
            Assert.DoesNotContain(remaining, x => x.RecordId is Positive or Negative);
            Assert.All(original.Where(x => x.RecordId is not (Positive or Negative)), row =>
                Assert.Contains(remaining, next => next.RecordId == row.RecordId &&
                    row.Fields.All(field => next.Fields[field.Key] == field.Value)));
            Assert.Equal(remaining.Count, backend.ReadBindings(backend.Import("controls", session.Export().Files)).Count);
            session.Discard();
            Assert.False(session.HasChanges);
            Assert.Equal(source, session.Export().Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>Failure after an eligible direction never leaks a partially cleared draft.</summary>
        [Theory]
        [InlineData("missing")]
        [InlineData("IncomStarshipInputConcepts.ConceptMenu.0")]
        public void Invalid_second_record_rolls_back_the_entire_clear(string invalid)
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var (session, _, source) = Open(issuer.Policy);
            var frozen = JsonSerializer.Serialize(session.Snapshot);
            var notifications = 0; session.Changed += (_, _) => notifications++;
            Assert.Throws<InvalidOperationException>(() => session.PreviewClearCapturedRecords([Positive, invalid]));
            Assert.Equal(frozen, JsonSerializer.Serialize(session.Snapshot));
            Assert.Equal(source, session.Export().Files[SquadronsProfileBackend.FileName]);
            Assert.False(session.HasChanges);
            Assert.Equal(0, notifications);
        }
        /// <summary>Empty or duplicate record sets are invalid and cannot stage anything.</summary>
        [Fact]
        public void Invalid_sets_fail_before_native_mutation()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var (session, _, source) = Open(issuer.Policy);
            Assert.Throws<ArgumentNullException>(() => session.PreviewClearCapturedRecords(null!));
            Assert.Throws<ArgumentException>(() => session.PreviewClearCapturedRecords([]));
            Assert.Throws<ArgumentException>(() => session.PreviewClearCapturedRecords([Positive, Positive]));
            Assert.Equal(source, session.Export().Files[SquadronsProfileBackend.FileName]);
            Assert.False(session.HasChanges);
        }
        /// <summary>A different draft revision invalidates the entire reviewed axis clear.</summary>
        [Fact]
        public void Stale_axis_review_cannot_remove_any_direction()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var (session, _, source) = Open(issuer.Policy);
            var review = session.PreviewClearCapturedRecords([Positive, Negative]);
            session.Rename("New revision");
            Assert.Throws<InvalidOperationException>(() => session.ApplyControllerChange(review));
            Assert.Equal(source, session.Export().Files[SquadronsProfileBackend.FileName]);
        }
        /// <summary>Single-record clearing delegates to the same review boundary and preserves the opposite direction.</summary>
        [Fact]
        public void Explicit_native_record_clear_remains_available()
        {
            using var issuer = new OwnerLicenseTestIssuer();
            var (session, backend, _) = Open(issuer.Policy);
            session.ApplyControllerChange(session.PreviewClearCapturedRecord(Positive));
            var remaining = backend.ReadBindings(session.Snapshot.Profile);
            Assert.DoesNotContain(remaining, x => x.RecordId == Positive);
            Assert.Contains(remaining, x => x.RecordId == Negative);
        }
        #endregion END Public
        #region BEGIN private helpers
        private static (GameProfileSession Session, SquadronsProfileBackend Backend, string Source) Open(EditionPolicy policy)
        {
            var source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Squadrons", "Synthetic_profile_synced"));
            var backend = new SquadronsProfileBackend();
            var profile = backend.Import("controls", new Dictionary<string, string> { [SquadronsProfileBackend.FileName] = source });
            var workspace = new GameWorkspace(1, Guid.NewGuid(), "Axis clear", profile, new());
            return (new(backend, workspace, policy, [], () => []), backend, source);
        }
        #endregion END private helpers
    }
}
