// Service tests manipulate static SetDBService() on model classes.
// Disable parallel execution across test classes to prevent cross-test interference.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]
