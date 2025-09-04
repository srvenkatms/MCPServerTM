# GitHub Actions CI/CD Pipeline

This repository now includes a complete CI/CD pipeline to build and deploy the MCP Server to Azure Container Registry.

## Files Added

- `Dockerfile` - Multi-stage Docker build for the .NET 8.0 MCP Server
- `.dockerignore` - Optimizes Docker build by excluding unnecessary files
- `.github/workflows/build-and-deploy.yml` - GitHub Actions workflow for CI/CD

## Workflow Details

The GitHub Actions workflow (`build-and-deploy.yml`) is triggered on:
- Push to main branch
- Pull requests to main branch

### Deployment Configuration

- **Registry**: `mcpcontainerreg.azurecr.io`
- **Image Name**: `imgmcpserver`
- **Authentication**: Uses `AZURE_CREDENTIALS` repository secret

### Image Tags

The workflow creates multiple tags for the Docker image:
- `latest` (for main branch)
- `main` (for main branch pushes)
- `pr-<number>` (for pull requests)
- `main-<sha>` (with commit SHA)

### Required Repository Secret

Ensure the `AZURE_CREDENTIALS` secret is configured with a JSON object containing:
```json
{
  "clientId": "your-service-principal-client-id",
  "clientSecret": "your-service-principal-client-secret",
  "tenantId": "your-tenant-id"
}
```

The service principal must have push permissions to the Azure Container Registry.

## Local Development

To build the Docker image locally:
```bash
docker build -t mcpserver .
```

To run the container:
```bash
docker run -p 8080:8080 mcpserver
```