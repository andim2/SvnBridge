namespace CodePlex.TfsLibrary.RegistrationWebSvc
{
    public interface IRegistrationWebSvc
    {
        FrameworkRegistrationEntry[] GetRegistrationEntries(string toolId);
    }
}