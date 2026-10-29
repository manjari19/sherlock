namespace Sherlock.Tests
{
    public static class TestHarness
    {
        private static int _passed = 0;
        private static int _failed = 0;

        public static void Run(string name, Action test)
        {
            try
            {
                test();
                _passed++;
                Console.WriteLine($"  PASS  {name}");
            }
            catch (Exception ex)
            {
                _failed++;
                Console.WriteLine($"  FAIL  {name}");
                Console.WriteLine($"        {ex.Message}");
            }
        }

        public static void AssertEqual<T>(T expected, T actual, string context = "")
        {
            if (!Equals(expected, actual))
                throw new Exception($"{context} Expected <{expected}> but got <{actual}>");
        }

        public static void AssertTrue(bool condition, string message)
        {
            if (!condition) throw new Exception($"Expected true: {message}");
        }

        public static int Summarize()
        {
            Console.WriteLine();
            Console.WriteLine($"{_passed} passed, {_failed} failed");
            return _failed == 0 ? 0 : 1;
        }
    }
}
