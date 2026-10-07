using System.Collections.Immutable;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

var path = args[0];
using var fs = File.OpenRead(path);
using var pe = new PEReader(fs);
var md = pe.GetMetadataReader();

string TypeName(TypeDefinitionHandle h)
{
    var td = md.GetTypeDefinition(h);
    var ns = md.GetString(td.Namespace);
    var n = md.GetString(td.Name);
    return string.IsNullOrEmpty(ns) ? n : ns + "." + n;
}

string BaseName(EntityHandle h)
{
    switch (h.Kind)
    {
        case HandleKind.TypeReference:
            var tr = md.GetTypeReference((TypeReferenceHandle)h);
            var ns = md.GetString(tr.Namespace);
            var n = md.GetString(tr.Name);
            return string.IsNullOrEmpty(ns) ? n : ns + "." + n;
        case HandleKind.TypeDefinition:
            return TypeName((TypeDefinitionHandle)h);
        default:
            return h.Kind.ToString();
    }
}

foreach (var tdh in md.TypeDefinitions)
{
    try
    {
    var td = md.GetTypeDefinition(tdh);
    if (td.IsNested) continue;
    var full = TypeName(tdh);
    if (!full.StartsWith("Avalonia.Android")) continue;
    var abs = (td.Attributes & System.Reflection.TypeAttributes.Abstract) != 0 ? "abstract " : "";
    var sealed_ = (td.Attributes & System.Reflection.TypeAttributes.Sealed) != 0 ? "sealed " : "";
    Console.WriteLine($"\n=== {full}  :  {BaseName(td.BaseType)}  [{abs}{sealed_}]");

    foreach (var mh in td.GetMethods())
    {
        var m = md.GetMethodDefinition(mh);
        var name = md.GetString(m.Name);
        if (name.StartsWith("<") || name.StartsWith("get_") || name.StartsWith("set_") || name.StartsWith("add_") || name.StartsWith("remove_")) continue;
        var attrs = m.Attributes;
        var vis = attrs & System.Reflection.MethodAttributes.MemberAccessMask;
        string acc = vis switch
        {
            System.Reflection.MethodAttributes.Public => "public",
            System.Reflection.MethodAttributes.Family => "protected",
            System.Reflection.MethodAttributes.FamORAssem => "protected internal",
            System.Reflection.MethodAttributes.Assembly => "internal",
            System.Reflection.MethodAttributes.Private => "private",
            _ => vis.ToString()
        };
        var virt = (attrs & System.Reflection.MethodAttributes.Virtual) != 0 ? "virtual " : "";
        var sign = m.DecodeSignature(new SigProvider(), null);
        Console.WriteLine($"    {acc} {virt}{sign.ReturnType} {name}({string.Join(", ", sign.ParameterTypes)})");
    }

    foreach (var ph in td.GetProperties())
    {
        var p = md.GetPropertyDefinition(ph);
        var ps = p.DecodeSignature(new SigProvider(), null);
        Console.WriteLine($"    PROP {ps.ReturnType} {md.GetString(p.Name)}");
    }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"    !! skip {tdh}: {ex.GetType().Name}");
    }
}

sealed class SigProvider : ISignatureTypeProvider<string, object>
{
    public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[]";
    public string GetByReferenceType(string elementType) => "ref " + elementType;
    public string GetFunctionPointerType(MethodSignature<string> signature) => "fnptr";
    public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) => genericType + "<" + string.Join(",", typeArguments) + ">";
    public string GetGenericMethodParameter(object genericContext, int index) => "!!" + index;
    public string GetGenericTypeParameter(object genericContext, int index) => "!" + index;
    public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;
    public string GetPinnedType(string elementType) => elementType;
    public string GetPointerType(string elementType) => elementType + "*";
    public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode.ToString();
    public string GetSZArrayType(string elementType) => elementType + "[]";
    public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) => reader.GetString(reader.GetTypeDefinition(handle).Name);
    public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) => reader.GetString(reader.GetTypeReference(handle).Name);
    public string GetTypeFromSpecification(MetadataReader reader, object genericContext, TypeSpecificationHandle handle, byte rawTypeKind) => "spec";
}
