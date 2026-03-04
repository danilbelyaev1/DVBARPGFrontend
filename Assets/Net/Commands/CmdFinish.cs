namespace DVBARPG.Net.Commands
{
    /// <summary>
    /// Команда досрочного завершения забега. Сервер закроет инстанс и отправит run/finish в Laravel.
    /// </summary>
    public sealed class CmdFinish : IClientCommand
    {
    }
}
