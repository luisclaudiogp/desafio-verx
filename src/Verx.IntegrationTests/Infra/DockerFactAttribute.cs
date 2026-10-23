using System.Diagnostics;

namespace Verx.IntegrationTests.Infra;

[AttributeUsage(AttributeTargets.Method)]
public sealed class DockerFactAttribute : FactAttribute
{
    private static readonly Lazy<bool> Disponivel = new(Verificar);

    public DockerFactAttribute()
    {
        if (!Disponivel.Value)
            Skip = "Docker nao esta disponivel; teste de integracao ignorado.";
    }

    public static bool DockerDisponivel => Disponivel.Value;

    private static bool Verificar()
    {
        try
        {
            using var processo = Process.Start(new ProcessStartInfo("docker", "info --format {{.ServerVersion}}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (processo is null) return false;
            if (!processo.WaitForExit(TimeSpan.FromSeconds(10)))
            {
                processo.Kill(true);
                return false;
            }
            return processo.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }
}
