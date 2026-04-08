/// <summary>
/// Implementar em objetos que o jogador pode inspecionar (tecla R).
/// </summary>
public interface IInspectable
{
    /// <summary>Chamado quando o jogador prime R enquanto olha para o objeto.</summary>
    InspectResult Inspect();
}
