using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.DotNet.Signatures;

// The pinned generator emits illegal generic pointer arguments in method-name lookup.
// Render the element type using its original renderer, then append pointer/byref suffixes.
static class PointerNameRepair
{
    public static int Run(string directory, bool repair)
    {
        int count=0;
        foreach(var file in Directory.GetFiles(directory,"*.dll"))
        {
            var module=ModuleDefinition.FromFile(file); int changed=0;
            foreach(var type in module.GetAllTypes()) foreach(var method in type.Methods)
            {
                if(method.CilMethodBody is not {} body) continue;
                for(int n=0;n<body.Instructions.Count;n++)
                {
                    var instruction=body.Instructions[n];
                    if(instruction.Operand is not MethodSpecification spec || spec.Signature is null) continue;
                    if(!spec.Signature.TypeArguments.Any(a=>a is PointerTypeSignature or ByReferenceTypeSignature)) continue;
                    if(!repair) throw new InvalidDataException($"Illegal generic pointer: {method.FullName}: {instruction}");
                    if(spec.Method?.Name!="RenderTypeName" || spec.Method.DeclaringType?.FullName!="Il2CppInterop.Runtime.IL2CPP" || spec.Signature.TypeArguments.Count!=1 || n==0)
                        throw new InvalidDataException("Unsupported illegal generic call: "+instruction);
                    var flag=body.Instructions[n-1];
                    if(flag.OpCode!=CilOpCodes.Ldc_I4_0 && flag.OpCode!=CilOpCodes.Ldc_I4_1) throw new InvalidDataException("Expected constant byref flag");
                    string suffix=flag.OpCode==CilOpCodes.Ldc_I4_1?"&":"";
                    TypeSignature element=spec.Signature.TypeArguments[0];
                    while(element is PointerTypeSignature pointer) {suffix="*"+suffix;element=pointer.BaseType;}
                    if(element is ByReferenceTypeSignature) throw new InvalidDataException("Unsupported byref generic");
                    if(element.FullName=="System.Void") {flag.OpCode=CilOpCodes.Nop;flag.Operand=null;instruction.OpCode=CilOpCodes.Ldstr;instruction.Operand="System.Void";}
                    else {flag.OpCode=CilOpCodes.Ldc_I4_0;flag.Operand=null;instruction.Operand=new MethodSpecification(spec.Method,new GenericInstanceMethodSignature(element));}
                    body.Instructions.Insert(++n,new CilInstruction(CilOpCodes.Ldstr,suffix));
                    body.Instructions.Insert(++n,new CilInstruction(CilOpCodes.Call,module.DefaultImporter.ImportMethod(typeof(string).GetMethod(nameof(string.Concat),new[]{typeof(string),typeof(string)})!)));
                    changed++; count++;
                }
            }
            if(changed>0) {var tmp=file+".repaired";module.Write(tmp);File.Move(tmp,file,true);}
        }
        if(repair) Run(directory,false);
        Console.WriteLine($"Pointer-name IL repair: {count} calls; illegal generic pointer scan passed.");
        return count;
    }
}


