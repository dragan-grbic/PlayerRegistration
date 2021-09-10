# PlayerRegistration


## Architecture
- Statefull **PlayerService** keeps player data
    - Partitioned
- Stateless **PlayerOrchestrator** 
  - Forward requests to appropriate partition
  - Scale in/out based on request per minute metrics

