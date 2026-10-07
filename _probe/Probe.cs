using System;
using System.Linq;
using System.Reflection;

public static class Probe
{
    public static void Dump()
    {
        var asm = typeof(Avalonia.Android.AvaloniaActivity).Assembly;
        foreach (var t in asm.GetExportedTypes().OrderBy(t => t.FullName))
        {
            Console.WriteLine($"TYPE {t.FullName} : {t.BaseType?.FullName}");
            foreach (var m in t.GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (m is MethodInfo mi && (mi.IsVirtual || mi.IsAbstract))
                {
                    Console.WriteLine($"    VIRTUAL {mi.ReturnType.Name} {mi.Name}({string.Join(", ", mi.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name))})");
                }
                if (m is PropertyInfo pi)
                {
                    Console.WriteLine($"    PROP {pi.PropertyType.Name} {pi.Name}");
                }
            }
        }
    }
}
