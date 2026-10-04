// UnitDataPaths.cs
// External override root for the data-driven unit files, per platform:
//   editor         <project>/units
//   Windows/Linux  <exeDir>/units
//   macOS          next to the .app bundle
//   Android/iOS    <persistentDataPath>/units
// Deploy by copying the exported folder (UnitsExternal/units) there.
using System.IO;
using UnityEngine;

public static class UnitDataPaths
{
    public static string ExternalRoot
    {
        get
        {
#if UNITY_EDITOR
            // <project>/Assets → <project>/units
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "units"));
#elif UNITY_ANDROID || UNITY_IOS
            return Path.Combine(Application.persistentDataPath, "units");
#elif UNITY_STANDALONE_OSX
            // player dataPath = Foo.app/Contents → two levels up sits next to Foo.app
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "units"));
#else
            // Windows/Linux: dataPath = <exeDir>/<Name>_Data → parent is the exe dir
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "units"));
#endif
        }
    }
}
