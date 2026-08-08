using MediatR;
using PortfolioCMS.Application.Common.Interfaces;
using PortfolioCMS.Application.DTOs;

namespace PortfolioCMS.Application.Features.AI;

// ─── Improve text ─────────────────────────────────────────────────────────────

public record ImproveTextCommand(ImproveTextRequest Payload) : IRequest<ImproveTextResponse>;

public sealed class ImproveTextHandler : IRequestHandler<ImproveTextCommand, ImproveTextResponse>
{
    private readonly IAiService _ai;
    public ImproveTextHandler(IAiService ai) => _ai = ai;

    public async Task<ImproveTextResponse> Handle(ImproveTextCommand request, CancellationToken ct)
    {
        var improved = await _ai.ImproveTextAsync(request.Payload.Text, request.Payload.Context, ct);
        return new ImproveTextResponse(request.Payload.Text, improved);
    }
}

// ─── Generate project description ────────────────────────────────────────────

public record GenerateProjectDescCommand(GenerateProjectDescRequest Payload)
    : IRequest<GenerateProjectDescResponse>;

public sealed class GenerateProjectDescHandler
    : IRequestHandler<GenerateProjectDescCommand, GenerateProjectDescResponse>
{
    private readonly IAiService _ai;
    public GenerateProjectDescHandler(IAiService ai) => _ai = ai;

    public async Task<GenerateProjectDescResponse> Handle(
        GenerateProjectDescCommand request, CancellationToken ct)
    {
        var (desc, tech) = await _ai.GenerateProjectDescriptionAsync(
            request.Payload.ReadmeContent, request.Payload.ProjectTitle, ct);
        return new GenerateProjectDescResponse(desc, tech);
    }
}
