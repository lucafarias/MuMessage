using MuLog;
using MuMessage;
using Ninject;
using Ninject.Modules;

public class MuModule : NinjectModule
{
    public override void Load()
    {
        // 1. DEVI scommentare questo. Ninject deve sapere come creare ILogManager
        // per poterlo passare al costruttore di MessageEngine
        Bind<ILogManager>().To<LogManager>().InSingletonScope();

        // 2. Lega MessageEngine
        Bind<MessageEngine>().ToSelf().InSingletonScope();
    }
}