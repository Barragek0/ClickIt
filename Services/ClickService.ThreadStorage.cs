namespace ClickIt.Services
{
    public partial class ClickService
    {
        internal static void ClearThreadLocalStorageForCurrentThread()
        {
            _threadGroundLabelEntityAddresses?.Clear();
            _threadGroundLabelEntityAddresses = null;

            _threadSkillBarEntriesBuffer?.Clear();
            _threadSkillBarEntriesBuffer = null;
        }

        internal static void ClearThreadLocalStorageForTests()
        {
            ClearThreadLocalStorageForCurrentThread();
        }
    }
}