// ABOUTME: Minimal GL shader-program wrapper with uniform-location caching.
// ABOUTME: Stand-in for UDB's GLShader/GLShaderManager pair until the full shader set is ported.

using Silk.NET.OpenGL;

namespace DBuilder.Rendering;

public sealed class Shader : IDisposable
{
    private readonly GL _gl;
    private readonly Dictionary<string, int> _uniformLocations = new();
    internal uint Program { get; private set; }

    public Shader(GL gl, string vertexSource, string fragmentSource)
    {
        _gl = gl;
        uint vs = Compile(ShaderType.VertexShader, vertexSource);
        uint fs = Compile(ShaderType.FragmentShader, fragmentSource);

        Program = _gl.CreateProgram();
        _gl.AttachShader(Program, vs);
        _gl.AttachShader(Program, fs);
        _gl.LinkProgram(Program);
        _gl.GetProgram(Program, ProgramPropertyARB.LinkStatus, out int linked);
        if (linked == 0)
        {
            string log = _gl.GetProgramInfoLog(Program);
            _gl.DeleteProgram(Program);
            throw new InvalidOperationException($"Shader link failed: {log}");
        }
        _gl.DetachShader(Program, vs);
        _gl.DetachShader(Program, fs);
        _gl.DeleteShader(vs);
        _gl.DeleteShader(fs);
    }

    private uint Compile(ShaderType type, string source)
    {
        uint id = _gl.CreateShader(type);
        _gl.ShaderSource(id, source);
        _gl.CompileShader(id);
        _gl.GetShader(id, ShaderParameterName.CompileStatus, out int ok);
        if (ok == 0)
        {
            string log = _gl.GetShaderInfoLog(id);
            _gl.DeleteShader(id);
            throw new InvalidOperationException($"{type} compile failed: {log}");
        }
        return id;
    }

    internal int Uniform(string name)
    {
        if (_uniformLocations.TryGetValue(name, out int loc)) return loc;
        loc = _gl.GetUniformLocation(Program, name);
        _uniformLocations[name] = loc;
        return loc;
    }

    public void Dispose()
    {
        if (Program != 0)
        {
            _gl.DeleteProgram(Program);
            Program = 0;
        }
        GC.SuppressFinalize(this);
    }
}
