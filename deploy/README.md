# Deploying Vakt using Docker Compose

If you are not a .NET developer and just want to run Vakt locally, you can use the provided Docker Compose file.

## Prerequisites
- [Docker Desktop](https://www.docker.com/products/docker-desktop) installed.

## Quick Start

1.  Navigate to this folder:
    ```bash
    cd deploy/docker
    ```
2.  Start the stack:
    ```bash
    docker-compose up -d
    ```
3.  Access the services:
    - **Proxy**: http://localhost:5000
    - **Intelligence**: http://localhost:8081

## Architecture
This setup launches:
- `vakt-proxy`: The gateway intercepting and redacting PII.
- `vakt-intelligence`: The local ML service (auto-downloads Phi-3 and Embedding models).
- `redis-stack`: The Vector Database for Semantic Caching.
