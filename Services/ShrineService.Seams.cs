using System.Runtime.CompilerServices;

namespace ClickIt.Services
{
    public partial class ShrineService
    {
        internal static void ClearThreadLocalStorageForTests()
        {
            _threadLocalShrineList = null;
        }

        internal static int GetThreadLocalShrineListInstanceIdForTests()
        {
            return RuntimeHelpers.GetHashCode(GetThreadLocalShrineList());
        }
    }
}
