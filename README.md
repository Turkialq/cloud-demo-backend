# WebSocket Chat Backend

A real-time chat server built with .NET 8 and WebSockets.

## Features

- Real-time WebSocket communication
- User join/leave notifications
- Active users list
- No authentication required (demo purposes)
- CORS enabled for cross-origin requests

## Requirements

- .NET 8 SDK

## Quick Start

### Local Development

```bash
# Install dependencies and run
dotnet run
```

The server will start on `http://localhost:5050`

### AWS VM Deployment

```bash
# Clone the repository
git clone <your-repo-url>
cd backend

# Make deploy script executable
chmod +x deploy.sh

# Run deployment script
./deploy.sh
```

## WebSocket Endpoint

- WebSocket URL: `ws://localhost:5050/ws`

## Message Format

```json
// Join message (client -> server)
{
  "type": "join",
  "username": "JohnDoe"
}

// Chat message (client -> server)
{
  "type": "message",
  "content": "Hello, World!"
}

// Message broadcast (server -> clients)
{
  "Type": "message",
  "Username": "JohnDoe",
  "Content": "Hello, World!",
  "Timestamp": "2024-01-01T12:00:00Z"
}
```

## Configuration

- Port: 5050 (configurable in Properties/launchSettings.json)
- CORS: Allows all origins (update for production)
