namespace CodePlex.TfsLibrary.RegistrationWebSvc
{
    public interface IRegistrationWebSvc
    {
        RegistrationEntry[] GetRegistrationEntries(string toolId);
    }
}