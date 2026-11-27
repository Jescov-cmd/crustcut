using FluentAssertions;
using PrimeOSTuner.Core.Games;
using PrimeOSTuner.Core.Lifecycle;
using Xunit;

namespace PrimeOSTuner.Tests.Games;

public class JavaGameHeuristicsTests
{
    [Theory]
    // Modrinth-launched Fabric — the real-world case that started this: hours of play
    // with the machine treated as idle, memory cleaner running mid-game.
    [InlineData(@"C:\Users\x\AppData\Roaming\ModrinthApp\meta\java_versions\zulu21\bin\javaw.exe -Xmx4096M -cp ... net.fabricmc.loader.impl.launch.knot.KnotClient")]
    [InlineData(@"javaw.exe -Djava.library.path=C:\mc\.minecraft\bin net.minecraft.client.main.Main")]
    [InlineData(@"java -cp forge.jar cpw.mods.bootstraplauncher.BootstrapLauncher")]
    [InlineData(@"javaw --add-opens ... org.quiltmc.loader.impl.launch.knot.KnotClient")]
    [InlineData(@"C:\Users\x\.lunarclient\jre\bin\javaw.exe com.moonsworth.lunar.genesis.Genesis")]
    public void Recognises_minecraft_in_java_command_lines(string cmd)
        => JavaGameHeuristics.IsMinecraft(cmd).Should().BeTrue();

    [Theory]
    // Java that is NOT a game must never trigger game mode: dev tooling runs javaw too.
    [InlineData(@"javaw.exe -jar C:\tools\jenkins-agent.jar")]
    [InlineData(@"java -cp gradle-launcher.jar org.gradle.launcher.daemon.bootstrap.GradleDaemon")]
    [InlineData(@"javaw.exe -Xmx512m -jar C:\Program Files\Apps\pdf-tool.jar")]
    // Minecraft MOD builds: the real command lines from his machine carry net.minecraft /
    // neoforged on the classpath but are Gradle, not a game (each became a 0-frame session).
    [InlineData(@"""C:\Program Files\Eclipse Adoptium\jdk-17\bin\java.exe"" -Xmx64m -Dorg.gradle.appname=gradlew -jar ""C:/Users/x/projects/Minecraft Mods/B.A.G/gradle/wrapper/gradle-wrapper.jar"" build")]
    [InlineData(@"java.exe --add-opens=java.base/java.lang=ALL-UNNAMED -cp C:\Users\x\.gradle\caches\neoforged\neoform\net.minecraft\client.jar;C:\Users\x\.gradle\wrapper\dists\gradle-9\lib\gradle-launcher.jar org.gradle.launcher.daemon.bootstrap.GradleDaemon 9.1")]
    [InlineData(@"java.exe -cp C:\Users\x\.gradle\caches\net.fabricmc\fabric-loader.jar worker.org.gradle.process.internal.worker.GradleWorkerMain 'Gradle Worker Daemon 3'")]
    [InlineData(@"java.exe -cp C:\mods\build\libs net.neoforged.devlaunch.Main --launchTarget forgeclientdev")]
    [InlineData(@"java.exe -Dfabric.development=true -cp x net.fabricmc.devlaunchinjector.Main")]
    [InlineData(@"java.exe -Dneoforge.enableGameTest=true -cp x net.neoforged.devlaunch.Main --launchTarget forgegametestserverdev")]
    [InlineData(@"java -Xmx4G -jar server.jar net.minecraft.server.Main --nogui")]
    [InlineData("")]
    [InlineData(null)]
    public void Ignores_non_game_java(string? cmd)
        => JavaGameHeuristics.IsMinecraft(cmd).Should().BeFalse();

    [Fact]
    public async Task Watcher_starts_and_stops_the_synthetic_game()
    {
        KnownGame? detected = JavaGameHeuristics.MinecraftJava;
        var watcher = new GameProcessWatcher(
            knownGamesProvider: () => Task.FromResult<IReadOnlyList<KnownGame>>(Array.Empty<KnownGame>()),
            processSnapshotProvider: () => Array.Empty<string>(),
            syntheticDetector: _ => detected);

        var started = new List<string>();
        var stopped = new List<string>();
        watcher.GameStarted += (_, g) => started.Add(g.Id);
        watcher.GameStopped += (_, e) => stopped.Add(e.Game.Id);

        await watcher.TickAsync();
        started.Should().Equal("java.minecraft");

        await watcher.TickAsync();               // still running — no duplicate event
        started.Should().HaveCount(1);

        detected = null;                          // Minecraft exited
        await watcher.TickAsync();
        stopped.Should().Equal("java.minecraft");
    }

    [Theory]
    [InlineData("javaw.exe -Xmx8G -Xms2G net.minecraft.client.main.Main", 8L << 30)]
    [InlineData("javaw.exe -Xms512m -Xmx6144m -cp x net.fabricmc", 6144L << 20)]
    [InlineData("\"C:\\java\\javaw.exe\" \"-Xmx4096M\" cpw.mods.bootstraplauncher", 4096L << 20)]
    public void Reads_how_much_memory_minecraft_is_allowed(string cmd, long expected)
        => JavaGameHeuristics.MaxHeapBytes(cmd).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("javaw.exe net.minecraft.client.main.Main")]
    [InlineData("javaw.exe -XmxGarbage")]
    public void No_heap_flag_means_unknown(string? cmd)
        => JavaGameHeuristics.MaxHeapBytes(cmd).Should().BeNull();
}
