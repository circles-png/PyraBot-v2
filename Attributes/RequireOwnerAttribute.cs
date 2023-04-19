using NetCord;
using NetCord.Services;

namespace PyraBot;

public class RequireOwnerAttribute<TContext>
    : PreconditionAttribute<TContext> where TContext : IContext, IUserContext
{
    public override ValueTask EnsureCanExecuteAsync(TContext context)
    {
        if (context.User.Id != PyraBot.Configuration.OwnerId)
            throw new("You need to be the owner of this bot to use this command.");

        return default;
    }
}
