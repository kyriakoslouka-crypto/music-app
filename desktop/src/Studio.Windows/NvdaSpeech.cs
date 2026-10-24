using System.Runtime.InteropServices;
namespace Studio.Windows;
internal static class NvdaSpeech
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CancelSpeech();
    public static void Cancel()
    {
        // Only an explicitly supplied, official DLL beside the EXE is considered.
        var path = Path.Combine(AppContext.BaseDirectory, "nvdaControllerClient64.dll");
        if (!File.Exists(path)) throw new InvalidOperationException("Δεν βρέθηκε το επίσημο nvdaControllerClient64.dll δίπλα στο EXE. Απενεργοποίησε την προαιρετική ακύρωση ομιλίας ή πρόσθεσε το επίσημο DLL.");
        var handle = NativeLibrary.Load(path);
        try
        {
            var function = Marshal.GetDelegateForFunctionPointer<CancelSpeech>(NativeLibrary.GetExport(handle, "nvdaController_cancelSpeech"));
            if (function() != 0) throw new InvalidOperationException("Το NVDA δεν αποκρίθηκε στην ακύρωση ομιλίας.");
        }
        finally { NativeLibrary.Free(handle); }
    }
}
