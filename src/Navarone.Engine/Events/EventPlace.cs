namespace Navarone.Engine.Events;

/// <summary>
/// Where an event happened: the repo's root, its main checkout, which every worktree of the repo shares; the worktree
/// `navarone` ran in; the branch checked out there, or null on a detached HEAD or outside git; and the full hash of
/// HEAD, or null outside git or before the first commit.
/// </summary>
public sealed record EventPlace(string Repo, string Worktree, string? Branch, string? Head)
{
    /// <summary>
    /// The place of the worktree at the given root. Outside git the repo is the worktree itself, with no branch and no
    /// HEAD.
    /// </summary>
    public static EventPlace Of(string worktree)
    {
        var root = Path.GetFullPath(worktree);
        var git = new Git(root);
        return new EventPlace(git.MainCheckout() ?? root, root, git.Branch(), git.Head());
    }
}
