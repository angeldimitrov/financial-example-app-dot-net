using Xunit;

namespace FinanceApp.Tests.UI;

/// <summary>
/// Collection definition for Playwright tests to ensure proper test isolation
/// Prevents concurrent execution of UI tests that might interfere with each other
/// </summary>
[CollectionDefinition("PlaywrightCollection", DisableParallelization = true)]
public class PlaywrightCollectionDefinition
{
    // This class is intentionally empty. It serves as a marker for the collection definition.
    // The DisableParallelization attribute ensures that Playwright tests run sequentially
    // to avoid browser/port conflicts.
}