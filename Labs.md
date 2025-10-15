# Clean Architecture Migration Workshop Labs

## Overview

Welcome to the Clean Architecture Mini-Workshop! In this hands-on workshop, you'll learn how to transform a tightly coupled, monolithic ASP.NET Core application into a well-structured Clean Architecture solution.

**Time Estimate:** 2 hours

## Learning Objectives

By the end of this workshop, you will be able to:
- Identify common architectural problems in tightly coupled applications
- Install and use the Ardalis Clean Architecture template
- Implement features using vertical slice architecture
- Apply CQRS patterns with Mediator
- Properly separate concerns across architectural layers
- Apply dependency injection and inversion of control principles

## Prerequisites

- .NET 10 SDK installed
- Visual Studio 2022 or VS Code with C# extension
- Basic understanding of ASP.NET Core MVC
- Familiarity with Entity Framework Core

## The Legacy Application

The `legacy/TightlyCoupled.WebShop` application is a deliberately poorly designed e-commerce application that exhibits many anti-patterns:

- **Hard-coded paths and configuration** throughout the codebase
- **Static utility classes** that violate dependency injection principles
- **Mixed concerns** - business logic, data access, and infrastructure all intertwined
- **Direct file system and database access** in controllers
- **No abstractions** - concrete implementations everywhere
- **Poor testability** - tightly coupled to external dependencies
- **Global state** - shared mutable state across the application

Your mission is to migrate this application to Clean Architecture!

---

## Workshop Structure

This workshop follows a **vertical slice** approach - instead of building entire layers at once, we'll implement complete features from UI to database one at a time. This mirrors real-world development and shows the full power of Clean Architecture.

### [Lab 1: Setup and Template Installation](Lab1.md)
**Time:** 15 minutes

Install the Ardalis Clean Architecture template and create a new solution structure. Explore the layers and understand the dependency rules.

**You will:**
- Install the Clean Architecture template
- Generate a new solution (which creates its own `src` folder structure)
- Understand the Core, UseCases, Infrastructure, and Web layers
- Learn the dependency inversion principle

### [Lab 2: Implement List Items Feature (Vertical Slice)](Lab2.md)
**Time:** 30 minutes

Implement your first complete feature - listing available items in the catalog. You'll work through all layers: domain model → repository → query → API endpoint.

**You will:**
- Create the `Item` entity in Core
- Define repository interfaces
- Implement the repository in Infrastructure
- Create a "List Items" query with Mediator
- Build an API endpoint in the Web layer
- Test the complete feature end-to-end

### [Lab 3: Implement Place Order Feature (Vertical Slice)](Lab3.md)
**Time:** 40 minutes

Build a more complex feature with business logic - placing an order. This introduces aggregates, domain events, and command handlers.

**You will:**
- Create the `Order` aggregate with business rules
- Implement a "Place Order" command
- Handle order validation
- Configure entity relationships in Infrastructure
- Create an API endpoint for order placement
- Add unit tests for the command handler

### [Lab 4: Refactor Legacy Code Patterns](Lab4.md)
**Time:** 25 minutes

Identify and replace anti-patterns from the legacy application. Learn how to properly handle cross-cutting concerns.

**You will:**
- Replace static utility classes with dependency injection
- Move hard-coded configuration to the Options pattern
- Implement proper logging with ILogger
- Create abstractions for external services
- Compare testability: legacy vs. clean architecture

### [Lab 5: Advanced Topics & Polish](Lab5.md)
**Time:** 10 minutes + Bonus Challenges

Wrap up with testing strategies, deployment considerations, and bonus challenges.

**You will:**
- Write integration tests
- Understand migration strategies for real applications
- Review SOLID principles in practice
- Explore bonus challenges (Specifications, FluentValidation, Domain Events)

---

## Workshop Approach: Vertical Slices

This workshop uses **vertical slice architecture** rather than building layer-by-layer. Here's why:

### Traditional Layer-First Approach ❌
1. Build all domain entities
2. Build all repositories
3. Build all use cases
4. Build all endpoints

**Problems:**
- Can't test anything until all layers are done
- Lose sight of actual features
- Harder to understand dependencies
- Doesn't match real development flow

### Vertical Slice Approach ✅
1. Pick a feature (e.g., "List Items")
2. Build entity → repository → query → endpoint for that feature
3. Test the complete feature
4. Move to next feature

**Benefits:**
- Working feature after each slice
- Clear understanding of data flow
- Easier to test and validate
- Matches agile development practices
- Demonstrates Clean Architecture patterns in context

---

## Getting Started

Ready to begin? Head over to **[Lab 1: Setup and Template Installation](Lab1.md)** to get started!

---

## Resources

- [Ardalis Clean Architecture Template](https://github.com/ardalis/CleanArchitecture)
- [Clean Architecture by Robert C. Martin](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Vertical Slice Architecture](https://jimmybogard.com/vertical-slice-architecture/)
- [Mediator Documentation](https://github.com/martinothamar/Mediator)
- [Ardalis Result Pattern](https://github.com/ardalis/Result)

---

*Need help? Ask your instructor or refer to the solution branch in the repository.*
