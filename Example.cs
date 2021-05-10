namespace DemoApp {
    /// <summary>
    /// This is the common interface for our examples. An example just has a
    /// description and provides a Run method in order to execute it.
    /// </summary>
    interface Example {
        string Description();

        void Run();

    }
}
