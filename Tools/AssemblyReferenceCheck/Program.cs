using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

if (args.Length != 1)
{
	Console.Error.WriteLine("Usage: AssemblyReferenceCheck <assembly-path>");
	return 2;
}

var forbiddenReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
	"UniTask",
	"UniTask.DOTween",
	"UniTask.Linq",
	"UniTask.TextMeshPro",
	"UnityToolkit",
	"ZString"
};

using FileStream stream = File.OpenRead(args[0]);
using var peReader = new PEReader(stream);
MetadataReader metadata = peReader.GetMetadataReader();

var found = new List<string>();
foreach (AssemblyReferenceHandle handle in metadata.AssemblyReferences)
{
	AssemblyReference reference = metadata.GetAssemblyReference(handle);
	string name = metadata.GetString(reference.Name);
	if (forbiddenReferences.Contains(name))
	{
		found.Add(name);
	}
}

if (found.Count > 0)
{
	Console.Error.WriteLine($"Forbidden runtime references found: {string.Join(", ", found)}");
	return 1;
}

Console.WriteLine("Verified: no UnityToolkit, UniTask, or ZString runtime references.");
return 0;
