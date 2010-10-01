using System;

namespace TestVisualStudioIntegration
{
    public class DefectiveMethodClass
    {
        public void CallingProblematicMethod()
        {
            System.GC.Collect();
        }
    }
}
