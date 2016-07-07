using System;
using System.Threading.Tasks;

namespace SNPPlib.Extensions
{
    internal static class TaskExtensions
    {
        //Fire-and-forget task, essentially the same as what is in Microsoft.VisualStudio.Threading do we want to include that instead?
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "task", Justification = "The parameter is supposed to be unused.")]
        public static void Forget(this Task task)
        {
        }
    }
}