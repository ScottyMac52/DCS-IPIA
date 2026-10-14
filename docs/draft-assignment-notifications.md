# Draft assignment notifications

`DraftAssignmentService.Changed` is the shared notification boundary for assignment edits. It fires synchronously once after the complete assignment snapshot and undo history have been updated by assign, remove, restore, move, copy, swap, axis tuning, or undo. A discard fires only if the assignment snapshot changes. Rejected, cancelled, unconfirmed, and unchanged operations do not publish a change.

The module workspace host subscribes to its current draft and updates pending status and validate/build/export availability before publishing the existing workspace context `StateChanged` event. Replacing a draft during structural edits, save, or discard detaches the old subscription and attaches the new one. Closing the host detaches its draft subscription.

Both command managers, including cached feature views, refresh usage and assignment-dependent filters from the current draft. They preserve valid command selections and search/category/type/state filters. The Assignments view preserves its device and modifier layer, and guards choice refreshes against recursive selection events. Initializing these views repeatedly with the same context does not add duplicate subscriptions.

Validation includes application tests for atomic notifications, undo, axis tuning, restore, discard, conflicts and no-op operations. The Windows WPF smoke suite exercises an assignment through the real callout and action button, cached navigation, undo, cancellation/rejection, filters, save, discard, and edits to replacement drafts, checking both command managers and all shell output actions.
