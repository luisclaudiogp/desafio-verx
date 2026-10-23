using System.Text.RegularExpressions;

namespace Verx.ArchitectureTests;

public partial class CleanCodeTests
{
    [GeneratedRegex(@"^(?:(?:public|internal|private|protected|sealed|static|abstract|partial|readonly|file)\s+)*(?:class|interface|record(?:\s+struct)?|struct|enum)\s+\w+", RegexOptions.Multiline)]
    private static partial Regex DeclaracaoDeTipo();

    [GeneratedRegex(@"(?:class|interface|record(?:\s+struct)?|struct|enum)\s+(\w+)")]
    private static partial Regex NomeDoTipo();

    private static string RaizDaSolucao()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Verx.Finance.slnx")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Raiz da solucao nao encontrada.");
    }

    private static IEnumerable<string> ArquivosDeProducao() =>
        Directory.EnumerateFiles(Path.Combine(RaizDaSolucao(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    [Fact]
    public void CadaArquivoDeclaraUmUnicoTipoDeNivelSuperior()
    {
        var violacoes = ArquivosDeProducao()
            .Select(f => (Arquivo: Path.GetRelativePath(RaizDaSolucao(), f), Tipos: DeclaracaoDeTipo().Count(File.ReadAllText(f))))
            .Where(x => x.Tipos > 1)
            .Select(x => $"{x.Arquivo} ({x.Tipos} tipos)")
            .ToList();

        Assert.True(violacoes.Count == 0, "Arquivos com mais de um tipo: " + string.Join("; ", violacoes));
    }

    [Fact]
    public void NomeDoArquivoCorrespondeAoTipoDeclarado()
    {
        var violacoes = new List<string>();
        foreach (var arquivo in ArquivosDeProducao())
        {
            var texto = File.ReadAllText(arquivo);
            var match = DeclaracaoDeTipo().Match(texto);
            if (!match.Success) continue;

            var nome = NomeDoTipo().Match(match.Value).Groups[1].Value;
            var esperado = Path.GetFileNameWithoutExtension(arquivo);
            if (!string.Equals(nome, esperado, StringComparison.Ordinal)
                && !arquivo.EndsWith("Program.cs", StringComparison.Ordinal))
                violacoes.Add($"{Path.GetRelativePath(RaizDaSolucao(), arquivo)} declara {nome}");
        }

        Assert.True(violacoes.Count == 0, "Nome do arquivo difere do tipo: " + string.Join("; ", violacoes));
    }
}
