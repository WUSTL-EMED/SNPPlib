using System;
using System.Threading.Tasks;

namespace SNPPlib
{
    public static class TaskExtensions
    {
        //Fire-and-forget task, essentially the same as what is in Microsoft.VisualStudio.Threading do we want to include that instead?
        public static void Forget(this Task task)
        {
        }
    }
}