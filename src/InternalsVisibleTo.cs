#nullable enable
// CivOne
//
// To the extent possible under law, the person who associated CC0 with
// CivOne has waived all copyright and related or neighboring rights
// to CivOne.
//
// You should have received a copy of the CC0 legalcode along with this
// work. If not, see <http://creativecommons.org/publicdomain/zero/1.0/>.

// Exposes the simulation layer's internal members (City.X, Game.AddCity, the COS
// helpers, etc.) to the test assembly. GenerateAssemblyInfo is disabled for this
// project, so the attribute is declared here rather than via the MSBuild item.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("CivOne.Tests")]
