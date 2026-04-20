using DevInsight.Domain.Enums;
namespace DevInsight.Application.Common;
public interface IGitProviderFactory
{
    IGitProviderService GetService(GitProvider provider);
}
