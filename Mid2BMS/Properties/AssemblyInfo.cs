using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

// ComVisible を false に設定すると、その型はこのアセンブリ内で COM コンポーネントから 
// 参照不可能になります。COM からこのアセンブリ内の型にアクセスする場合は、
// その型の ComVisible 属性を true に設定してください。
[assembly: ComVisible(false)]
[assembly: SupportedOSPlatform("windows7.0")]

// 次の GUID は、このプロジェクトが COM に公開される場合の、typelib の ID です
[assembly: Guid("dae8665a-4da5-4f08-8fc3-d400f06b162b")]

// Characterization tests exercise the legacy pipeline without making its
// internal implementation part of the public API.
[assembly: InternalsVisibleTo("Mid2BMS.CharacterizationTests")]
