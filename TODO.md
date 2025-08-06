# TODO

## Immediate

- [x] Input validation on registration in backend
- [ ] Input validation on login form in frontend
- [ ] Secure Admin role registration somehow
- [ ] Example on how to generate/create the api secret for jwt in the readme for kubernetes

## Design Patterns Implementation (GoF Book)

### Next: Repository Pattern with Abstract Factory

- [x] Create `IPostRepository` interface for data access abstraction
  - Defines contract for post data operations, enabling dependency inversion
- [x] Implement `JsonPlaceholderPostRepository` (current external API)
  - Encapsulates current HTTP API calls, making external dependency explicit
- [ ] Test builder needs more interfaces?
- [ ] Implement `MockPostRepository` for testing/demo (use the in-mem DB)
  - Provides in-memory data for development/testing without external dependencies
  - Demonstrates strategy pattern - same interface, different behavior
- [ ] Create `IPostRepositoryFactory` interface
  - Abstract Factory pattern - creates families of related repository objects
- [ ] Implement `PostRepositoryFactory` with environment-based creation logic
  - Centralizes object creation decisions based on configuration/environment
  - Eliminates need for controllers to know about concrete implementations
- [ ] Refactor `PostsController` to use factory pattern instead of direct HttpClient
  - Removes tight coupling to HttpClient, makes controller more testable
  - Controller depends on abstractions, not concrete implementations
- [ ] Add configuration to switch between repository implementations
  - Runtime strategy switching based on environment or feature flags
- [ ] Test both local debugging and Kubernetes deployment scenarios
  - Validates factory creates correct implementations in different environments

**Benefits**: Loose coupling, testability, strategy switching, dependency inversion principle

**GoF Patterns Demonstrated**:
- **Repository Pattern**: Encapsulates data access logic
- **Abstract Factory**: Creates families of related objects (repositories)
- **Strategy Pattern**: Interchangeable algorithms/implementations
- **Dependency Injection**: Constructor injection of factory interface

### Frontend Pattern Visualizations (Future)

#### Observer Pattern - Live Updates & Notifications

- [ ] Create real-time notification system with multiple subscriber components
  - Animate observers being notified when posts are updated
  - Multiple UI panels reacting to same data source changes
  - Visual representation of publisher-subscriber relationships

#### State Pattern - Interactive State Machines

- [ ] Build animated post workflow (Draft → Review → Published → Archived)
  - Visual state transitions with CSS animations
  - Different UI behavior/appearance per state
  - State diagram overlay showing current position

#### Command Pattern - Undo/Redo System
- [ ] Implement post editing with visual undo/redo stack
  - Command history visualization as animated stack
  - Undo/redo animations with state rollback
  - Macro recording and playback for complex operations

#### Decorator Pattern - Dynamic UI Enhancement
- [ ] Create post cards that can be dynamically decorated
  - Add borders, badges, effects, themes at runtime
  - Visual layering animation showing decoration application
  - Combine multiple decorators with stacking animation

#### Chain of Responsibility - Request Processing Pipeline
- [ ] Visualize post validation/processing pipeline
  - Animated request flowing through validation handlers
  - Visual feedback for each step (success/failure states)
  - Handler chain modification with drag-and-drop

#### Strategy Pattern - Algorithm Visualization
- [ ] Implement multiple post sorting/filtering strategies with animations
  - Visual comparison of different sorting algorithms
  - Strategy switching with smooth transitions
  - Performance metrics display for each strategy

#### Builder Pattern - Complex Object Construction
- [ ] Create visual post builder with step-by-step assembly
  - Animated component addition to build complex posts
  - Progress indicators for each construction step
  - Different builder implementations for different post types

## Testing Strategy (Native .NET 9 Only)

### Backend Pattern Testing

- [ ] Create test project using MSTest framework (native to .NET)
  - Test Repository pattern implementations with different data sources
  - Verify Abstract Factory creates correct repository types based on configuration
  - Test Strategy pattern switching between different sorting/filtering algorithms

- [ ] Unit tests for Repository Pattern
  - Test `MockPostRepository` returns expected in-memory data
  - Test `JsonPlaceholderPostRepository` handles HTTP failures gracefully
  - Verify both implementations satisfy `IPostRepository` contract identically

- [ ] Integration tests using ASP.NET Core TestHost
  - Test full API pipeline with different repository configurations
  - Verify environment-based factory selection works in test scenarios
  - Test HTTP client configuration switching (local vs Kubernetes URLs)

- [ ] Factory Pattern validation tests
  - Test factory creates correct implementation based on configuration
  - Test factory handles invalid configuration gracefully
  - Verify dependency injection works with factory-created objects

### Frontend Component Testing (Native Blazor Testing)

- [ ] Create Blazor component tests using built-in TestHost
  - Test Posts component renders correctly with mock data
  - Test error states and loading states display properly
  - Test retry logic behavior without external dependencies

- [ ] Pattern behavior testing in components
  - Test Strategy pattern UI switching (different post display modes)
  - Test State pattern transitions in interactive components
  - Test Observer pattern notifications update multiple UI components

- [ ] Component interaction testing
  - Test modal opening/closing behavior
  - Test component communication through events
  - Test form validation and submission flows

### Functional Integration Testing
- [ ] End-to-end API testing without external dependencies
  - Use TestServer to test complete request/response cycles
  - Test configuration-based behavior switching
  - Verify logging and telemetry integration

- [ ] Cross-component pattern testing
  - Test backend Factory + frontend Strategy pattern integration
  - Test Repository pattern data flows to Blazor components
  - Verify pattern implementations work together correctly

## Prod relevant stuff

- [ ] Add security to the health endpoints