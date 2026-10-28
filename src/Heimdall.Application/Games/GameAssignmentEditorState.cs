namespace Heimdall.Application.Games
{
    /// <summary>Deterministic action availability and adjacent feedback shared by native assignment editors.</summary>
    public sealed record GameAssignmentEditorState(GameAssignmentActions Actions, string Feedback)
    {
        /// <summary>Combines native review with current editing/testing state without staging any changes.</summary>
        public static GameAssignmentEditorState For(bool editable, GameAssignmentReview review,
            bool assigned, bool ready, bool running, bool busy, string changedMessage)
        {
            var actions = GameAssignmentActions.For(editable, review.Valid, review.Changed, assigned, ready, running, busy);
            var feedback = running ? "Stop input testing to apply or clear assignments."
                : !review.Valid ? review.Message : review.Changed ? changedMessage : "Assignment unchanged.";
            return new(actions, feedback);
        }
    }
}
