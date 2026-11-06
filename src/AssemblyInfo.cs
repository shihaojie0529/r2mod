using System.Security;
using System.Security.Permissions;

// The RoR2 reference package publicizes members that retain their original visibility in game.
[module: UnverifiableCode]
#pragma warning disable CS0618 // Unity's Mono runtime reads this legacy assembly declaration.
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618
