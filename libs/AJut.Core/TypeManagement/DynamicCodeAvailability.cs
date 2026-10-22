namespace AJut.TypeManagement
{
    using System;
    using System.Runtime.CompilerServices;

    // Whether this runtime can make code at runtime, which is what building a generic type or method from a Type needs
    //  when it was never compiled ahead of time. It is RuntimeFeature.IsDynamicCodeSupported: false on an iOS build
    //  compiled ahead of time without the interpreter, true anywhere with a JIT. The test runner always has a JIT, so
    //  tests stand in for the other kind of platform with SimulateUnsupported, on their own thread only.
    internal static class DynamicCodeAvailability
    {
        [ThreadStatic]
        private static bool g_isSimulatingUnsupported;

        // ===========[ Properties ]============================================
        /// <summary>Whether code can be made at runtime here: false on an iOS build compiled ahead of time</summary>
        public static bool IsSupported => !g_isSimulatingUnsupported && RuntimeFeature.IsDynamicCodeSupported;

        // ===========[ Public Interface Methods ]==============================
        /// <summary>For tests: this thread acts as a platform without dynamic code until the result is disposed</summary>
        public static IDisposable SimulateUnsupported ()
        {
            var scope = new SimulationScope(g_isSimulatingUnsupported);
            g_isSimulatingUnsupported = true;
            return scope;
        }

        // ===========[ Subclasses/structs ]====================================
        private sealed class SimulationScope : IDisposable
        {
            private readonly bool m_wasSimulating;

            public SimulationScope (bool wasSimulating)
            {
                m_wasSimulating = wasSimulating;
            }

            public void Dispose ()
            {
                g_isSimulatingUnsupported = m_wasSimulating;
            }
        }
    }
}
