[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$OutputPath)
$ErrorActionPreference = 'Stop'
if (-not ('DuneShaderCompiler' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

public static class DuneShaderCompiler
{
    [ComImport, Guid("8BA5FB08-5195-40e2-AC58-0D989C3A0102"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IBlob
    {
        [PreserveSig] IntPtr GetBufferPointer();
        [PreserveSig] UIntPtr GetBufferSize();
    }
    [DllImport("d3dcompiler_47.dll", CallingConvention = CallingConvention.StdCall)]
    static extern int D3DCompile(byte[] source, UIntPtr length, [MarshalAs(UnmanagedType.LPStr)] string name,
        IntPtr defines, IntPtr include, [MarshalAs(UnmanagedType.LPStr)] string entry,
        [MarshalAs(UnmanagedType.LPStr)] string target, uint flags, uint effectFlags,
        out IBlob code, out IBlob errors);

    public static void Compile(string sourcePath, string outputPath)
    {
        byte[] source = Encoding.UTF8.GetBytes(File.ReadAllText(sourcePath));
        IBlob code = null, errors = null;
        try
        {
            int result = D3DCompile(source, (UIntPtr)source.Length, sourcePath, IntPtr.Zero, IntPtr.Zero,
                "main", "ps_2_0", 0, 0, out code, out errors);
            if (result < 0) throw new InvalidOperationException(errors == null ? "Shader compilation failed." :
                Marshal.PtrToStringAnsi(errors.GetBufferPointer(), checked((int)errors.GetBufferSize().ToUInt64())));
            byte[] bytes = new byte[checked((int)code.GetBufferSize().ToUInt64())];
            Marshal.Copy(code.GetBufferPointer(), bytes, 0, bytes.Length);
            File.WriteAllBytes(outputPath, bytes);
        }
        finally
        {
            if (code != null) Marshal.ReleaseComObject(code);
            if (errors != null) Marshal.ReleaseComObject(errors);
        }
    }
}
'@
}
[DuneShaderCompiler]::Compile((Join-Path $PSScriptRoot '../MaterialExtension/Rendering/MaterialColor.hlsl'), $OutputPath)
