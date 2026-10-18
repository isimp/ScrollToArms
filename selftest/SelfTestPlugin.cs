using System.Collections;
using BepInEx;

namespace ScrollToArms.SelfTest
{
    /// <summary>
    /// The self-test as a plugin of its own beside ScrollToArms. Typing scrolltoarms selftest in the
    /// console, or with a slash in chat, checks what the unit tests cannot reach in the world it is
    /// in. A release has no such command: it comes with this plugin alone.
    /// </summary>
    [BepInPlugin(Guid, "ScrollToArms self-test", Plugin.Version)]
    [BepInDependency(Plugin.Guid)]
    [BepInProcess("valheim.exe")]
    public sealed class SelfTestPlugin : BaseUnityPlugin
    {
        public const string Guid = "isimp.ScrollToArms.SelfTest";

        private bool _running;

        private void Awake()
        {
            // Registered with isCheat false, which also makes it reachable from chat with a slash.
            _ = new Terminal.ConsoleCommand("scrolltoarms", "ScrollToArms' development commands: selftest.", args =>
            {
                if (args.Length < 2 || args[1] != "selftest")
                {
                    args.Context.OrNull()?.AddString("Usage: scrolltoarms selftest");
                    return;
                }

                if (_running)
                {
                    args.Context.OrNull()?.AddString("The ScrollToArms self-test is already running.");
                    return;
                }

                if (Player.m_localPlayer == null || Player.m_localPlayer.IsDead())
                {
                    args.Context.OrNull()?.AddString("The ScrollToArms self-test runs in a world, with you alive in it.");
                    return;
                }

                args.Context.OrNull()?.AddString("ScrollToArms self-test running; close the console. Results go to LogOutput.log and the screen.");
                StartCoroutine(Run());
            });
        }

        private IEnumerator Run()
        {
            _running = true;
            yield return new Tests(Logger).Run();
            _running = false;
        }
    }
}
