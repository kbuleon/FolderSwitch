// The debug log is one static object of the application: its tests open, roll and close it one after the other, and
// nothing else may write to it meanwhile.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
