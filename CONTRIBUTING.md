# Contributing to StreamActions-Bot

:+1::tada: First off, thanks for taking the time to contribute! :tada::+1:

The following is a set of guidelines and rules for contributing to StreamActions-Bot and its packages, which are hosted in the [StreamActions Organization](https://github.com/StreamActions) on GitHub. Use your best judgment, and feel free to propose changes to this document in a pull request.

#### Table Of Contents

[Code of Conduct](#code-of-conduct)

[I don't want to read this whole thing, I just have a question!!!](#i-dont-want-to-read-this-whole-thing-i-just-have-a-question)

[How Can I Contribute?](#how-can-i-contribute)
  * [Reporting Bugs](#reporting-bugs)
  * [Suggesting Enhancements](#suggesting-enhancements)
  * [Your First Code Contribution](#your-first-code-contribution)
  * [Pull Requests](#pull-requests)

[Styleguides](#styleguides)
  * [Git Commit Messages](#git-commit-messages)
  * [C# Styleguide](#c-styleguide)

[Additional Notes](#additional-notes)
  * [Issue and Pull Request Labels](#issue-and-pull-request-labels)
  
[Acknowledgements](#acknowledgements)

## Code of Conduct

This project and everyone participating in it is governed by the [StreamActions-Bot Code of Conduct](CODE_OF_CONDUCT.md). By participating, you are expected to uphold this code. Please report unacceptable behavior to [streamactionshelp@gmail.com](mailto:streamactionshelp@gmail.com).

## I don't want to read this whole thing I just have a question!!!

> **Note:** Please don't file an issue to ask a question. You'll get faster results by using the resources below.

We have an official Discord if you have questions.

* [StreamActions Discord](https://discord.gg/awAuH8W)

## How Can I Contribute?

### Reporting Bugs

This section guides you through submitting a bug report for StreamActions-Bot. Following these guidelines helps maintainers and the community understand your report :pencil:, reproduce the behavior :computer: :computer:, and find related reports :mag_right:.

Before creating bug reports, please check [this list](#before-submitting-a-bug-report) as you might find out that you don't need to create one. When you are creating a bug report, please [include as many details as possible](#how-do-i-submit-a-good-bug-report). Fill out [the required template](.github/ISSUE_TEMPLATE/bug_report.md), the information it asks for helps us resolve issues faster.

> **Note:** If you find a **Closed** issue that seems like it is the same thing that you're experiencing, open a new issue and include a link to the original issue in the body of your new one.

#### Before Submitting A Bug Report

* **Perform a [cursory search](https://github.com/search?q=+is%3Aissue+user%3AStreamActions)** to see if the problem has already been reported. If it has **and the issue is still open**, add a comment to the existing issue instead of opening a new one.

#### How Do I Submit A (Good) Bug Report?

Bugs are tracked as [GitHub issues](https://guides.github.com/features/issues/). Create an issue on the repository and provide the following information by filling in [the template](.github/ISSUE_TEMPLATE/bug_report.md).

Explain the problem and include additional details to help maintainers reproduce the problem:

* **Use a clear and descriptive title** for the issue to identify the problem.
* **Describe the exact steps which reproduce the problem** in as many details as possible. For example, start by explaining how you started StreamActions-Bot, e.g. which command exactly you used in the terminal, or how you started StreamActions-Bot otherwise. When listing steps, **don't just say what you did, but explain how you did it**. For example, if you executed a chat command, explain if you used the console, Twitch chat, Discord, or GraphQL.
* **Provide specific examples to demonstrate the steps**. Include links to files or GitHub projects, or copy/pasteable snippets, which you use in those examples. If you're providing snippets in the issue, use [Markdown code blocks](https://help.github.com/articles/markdown-basics/#multiple-lines).
* **Describe the behavior you observed after following the steps** and point out what exactly is the problem with that behavior.
* **Explain which behavior you expected to see instead and why.**
* **Include screenshots and animated GIFs** which show you following the described steps and clearly demonstrate the problem.  You can use [this tool](https://www.cockos.com/licecap/) to record GIFs on macOS and Windows, and [this tool](https://github.com/colinkeenan/silentcast) or [this tool](https://github.com/GNOME/byzanz) on Linux.
* **If you're reporting that StreamActions-Bot crashed**, include a crash report with a stack trace from the operating system. Include the crash report in the issue in a [code block](https://help.github.com/articles/markdown-basics/#multiple-lines), a [file attachment](https://help.github.com/articles/file-attachments-on-issues-and-pull-requests/), or put it in a [gist](https://gist.github.com/) and provide link to that gist.
* **If the problem is related to performance or memory**, include a [CPU profile capture](https://docs.microsoft.com/en-us/dotnet/core/diagnostics/) with your report.
* **If the problem wasn't triggered by a specific action**, describe what you were doing before the problem happened and share more information using the guidelines below.

Provide more context by answering these questions:

* **Did the problem start happening recently** (e.g. after updating to a new version of StreamActions-Bot) or was this always a problem?
* If the problem started happening recently, **can you reproduce the problem in an older version of StreamActions-Bot?** What's the most recent version in which the problem doesn't happen? You can download older versions of StreamActions-Bot from [the releases page](https://github.com/StreamActions/StreamActions-Bot/releases).
* **Can you reliably reproduce the issue?** If not, provide details about how often the problem happens and under which conditions it normally happens.

Include details about your configuration and environment:

* **Which version of StreamActions-Bot are you using?**
* **What's the name and version of the OS you're using**?
* **Are you running StreamActions-Bot in a virtual machine?** If so, which VM software are you using and which operating systems and versions are used for the host and the guest?
* **Are you using a remote MongoDB?** If so, does reconfiguring to run MongoDB on the same machine as the bot fix the issue?
* **Are you running the bot in multiple channels from a single instance?** If so, does reconfiguring to only run in a single channel fix the issue?

### Suggesting Enhancements

This section guides you through submitting an enhancement suggestion for StreamActions-Bot, including completely new features and minor improvements to existing functionality. Following these guidelines helps maintainers and the community understand your suggestion :pencil: and find related suggestions :mag_right:.

Before creating enhancement suggestions, please check [this list](#before-submitting-an-enhancement-suggestion) as you might find out that you don't need to create one. When you are creating an enhancement suggestion, please [include as many details as possible](#how-do-i-submit-a-good-enhancement-suggestion). Fill in [the template](.github/ISSUE_TEMPLATE/feature_request.md), including the steps that you imagine you would take if the feature you're requesting existed.

#### Before Submitting An Enhancement Suggestion

* **Perform a [cursory search](https://github.com/search?q=+is%3Aissue+user%3AStreamActions)** to see if the enhancement has already been suggested. If it has, add a comment to the existing issue instead of opening a new one.

#### How Do I Submit A (Good) Enhancement Suggestion?

Enhancement suggestions are tracked as [GitHub issues](https://guides.github.com/features/issues/). Create an issue on the repository and provide the following information:

* **Use a clear and descriptive title** for the issue to identify the suggestion.
* **Provide a step-by-step description of the suggested enhancement** in as many details as possible.
* **Provide specific examples to demonstrate the steps**. Include copy/pasteable snippets which you use in those examples, as [Markdown code blocks](https://help.github.com/articles/markdown-basics/#multiple-lines).
* **Describe the current behavior** and **explain which behavior you expected to see instead** and why.
* **Include screenshots and animated GIFs** which help you demonstrate the steps or point out the part of StreamActions-Bot which the suggestion is related to. You can use [this tool](https://www.cockos.com/licecap/) to record GIFs on macOS and Windows, and [this tool](https://github.com/colinkeenan/silentcast) or [this tool](https://github.com/GNOME/byzanz) on Linux.
* **Explain why this enhancement would be useful** to most StreamActions-Bot users and isn't something that can or should be implemented as a community package.
* **List some other bots where this enhancement exists**, if available.
* **Specify which version of StreamActions-Bot you're using.**
* **Specify the name and version of the OS you're using.**

### Your First Code Contribution

Unsure where to begin contributing to StreamActions-Bot? You can start by looking through these [Help wanted issues][help-wanted] issues.

The issue lists are sorted by total number of comments. While not perfect, number of comments is a reasonable proxy for impact a given change will have.

### Pull Requests

The process described here has several goals:

- Maintain StreamActions-Bot's quality
- Fix problems that are important to users
- Engage the community in working toward the best possible StreamActions-Bot
- Enable a sustainable system for StreamActions-Bot's maintainers to review contributions

Please follow these steps to have your contribution considered by the maintainers:

1. Follow all instructions in [the template](PULL_REQUEST_TEMPLATE.md)
2. Follow the [styleguides](#styleguides)
3. After you submit your pull request, verify that all [status checks](https://help.github.com/articles/about-status-checks/) are passing <details><summary>What if the status checks are failing?</summary>If a status check is failing, and you believe that the failure is unrelated to your change, please leave a comment on the pull request explaining why you believe the failure is unrelated. A maintainer will re-run the status check for you. If we conclude that the failure was a false positive, then we will open an issue to track that problem with our status check suite.</details>

While the prerequisites above must be satisfied prior to having your pull request reviewed, the reviewer(s) may ask you to complete additional design work, tests, or other changes before your pull request can be ultimately accepted.

## Styleguides

### Git Commit Messages

* Use the present tense ("Add feature" not "Added feature")
* Use the imperative mood ("Move cursor to..." not "Moves cursor to...")
* Limit the first line to 72 characters or less
* Reference issues and pull requests liberally after the first line
* Consider starting the commit message with an applicable emoji:
    * :art: `:art:` when improving the format/structure of the code
    * :racehorse: `:racehorse:` when improving performance
    * :non-potable_water: `:non-potable_water:` when plugging memory leaks
    * :memo: `:memo:` when writing docs
    * :penguin: `:penguin:` when fixing something on Linux
    * :apple: `:apple:` when fixing something on macOS
    * :checkered_flag: `:checkered_flag:` when fixing something on Windows
    * :bug: `:bug:` when fixing a bug
    * :fire: `:fire:` when removing code or files
    * :green_heart: `:green_heart:` when fixing the CI build
    * :white_check_mark: `:white_check_mark:` when adding tests
    * :lock: `:lock:` when dealing with security
    * :arrow_up: `:arrow_up:` when upgrading dependencies
    * :arrow_down: `:arrow_down:` when downgrading dependencies
    * :shirt: `:shirt:` when removing linter warnings

### C# Styleguide

All C# must adhere to [Microsoft .NET Framework Design Guidelines](https://docs.microsoft.com/en-us/dotnet/standard/design-guidelines/).

#### Types and Type Members
* Insert XML Documentation tags on all types and type members
* Place _using_ statements in lexicographical order
* Use _#region_ blocks, and place members within a region in lexicographical order
* Place regions in the following order:
    * Sort first by access level
         * public
         * protected
         * internal
         * protected internal
         * private
         * private protected
    * Sort second by type (within the access level's position)
         * Fields
         * Constructors
         * Destructors
         * Delegates
         * Events
         * Enums
         * Interfaces
         * Properties
         * Indexers
         * Methods
         * Structs
         * Classes
     * Custom regions are okay, as long as they still follow the ordering scheme
         
#### Code
* Avoid platform-dependent code
* P/Invoke is strictly prohibited
* All files must have the license header added as a comment at the top, if the file format allows
* Qualify field, property, method, and event access with `this.`
* Prefer predefined types (string, int, etc)
* Use explicit types instead of `var`
* Always use braces
* Always use parenthesis for clarity in arithmetic, binary, and relational operators
* Prefer object/collection initializer pattern over `new Object(); Object.Property = value;`
* Prefer pattern matching over `is` with cast or null checks
* Prefer conditional expressions, where practical
* Prefer compound assignments, index operators, and rage operators
* Use expression bodies whenever possible
* Use the `_` discard operator when an output is not being used
* Prefer null coalesce and conditional calling, where possible
* Use `is null` for reference equality checks
* Prefer `readonly` fields
* Avoid unused parameters

#### Spacing, Indenting, New Lines
* No trailing whitespace
* Indents must be 4 spaces, no tabs
* Indent block contents, case contents, and case labels, but not braces
* Place open braces, `else`, `catch`, and `finally` on new lines
* Place members in object initializers and anonymous types on new lines
* Place query expression clauses on new lines
* Insert a space after keywords flow control statements
* Insert a space both before and after colon for base or interface in type declaration
* Insert a space after a comma
* Insert a space after each semicolon in `for`
* Insert a space both before and after binary operators

#### Naming
All members must follow _Microsoft.Naming_, defined in the Microsoft .NET Framework Design Guidelines

* Interfaces must start with `I`
* Types, non-field members, namespaces, delegates, and public or protected fields must use PascalCase
* Parameters must use camelCase
* Private or internal fields must use camelCase with a `_` prefix

#### XML Documentation
* Prefer using `<see>` and `<paramref>` tags when referencing types or params from a non-Microsoft type or member
* Always include a `<exception>` tag if the member throws exceptions
* Prefer wrapping type keywords, such as _true_, _false_, and _null_ in `<c>` inline code tags
* End all XMLDoc tags with a period
* Allowing tags to be inherited is okay
* Describing a constructor as just `Constructor` is okay, unless it is doing something complex that should be noted

#### Follow The Styleguide, The Easy Way
To make following the C# styleguide easier, we recommend using [Visual Studio 2019](https://visualstudio.microsoft.com/) with the following extensions:
 * CodeMaid
 * License Header Manager
 * Microsoft Code Analysis 2019
 * Trailing Whitespace Visualizer
 
 Our included _.editorconfig_ and _StreamActions.ruleset_ files will setup Visual Studio to guide you through these style requirements. Our included _CodeMaid.config_ will setup CodeMaid to correct some common issues on save.
 
 Our included _StreamActions-Bot.licenseheader_ file allows you to right-click a _.cs_ file and select to have _License Header Manager_ automatically insert the license header for you.
 
 NOTE: Our CodeMaid config will only regionize and then re-order regions the first time the file is saved. You must use the _Remove All Regions (CTRL+M, R)_ feature to remove regions, then re-save to have it run again.
 
## Additional Notes

### Issue and Pull Request Labels

This section lists the labels we use to help us track and manage issues and pull requests.

[GitHub search](https://help.github.com/articles/searching-issues/) makes it easy to use labels for finding groups of issues or pull requests you're interested in. For example, you might be interested in [open pull requests which haven't been reviewed yet](https://github.com/search?utf8=%E2%9C%93&q=is%3Aopen+is%3Apr+repo%3AStreamActions%2FStreamActions-Bot+comments%3A0). To help you find issues and pull requests, each label is listed with search links for finding open items with that label. We  encourage you to read about [other search filters](https://help.github.com/articles/searching-issues/) which will help you write more focused queries.

#### Type of Issue and Issue State

| Label name | :mag_right: | Description |
| --- | --- | --- |
| `bug` | [search][search-streamactions-repo-label-bug] | Something isn't working |
| `documentation` | [search][search-streamactions-repo-label-documentation] | Improvements or additions to documentation |
| `duplicate` | [search][search-streamactions-repo-label-duplicate] | This issue or pull request already exists |
| `enhancement` | [search][search-streamactions-repo-label-enhancement] | New features or feature requests. |
| `help wanted` | [search][search-streamactions-repo-label-help-wanted] | Extra attention is needed |
| `invalid` | [search][search-streamactions-repo-label-invalid] | This doesn't seem right |
| `question` | [search][search-streamactions-repo-label-question] | Further information is requested |
| `refactor` | [search][search-streamactions-repo-label-refactor] | Time to rewrite it |
| `testing` | [search][search-streamactions-repo-label-testing] | Something needs testing |
| `wontfix` | [search][search-streamactions-repo-label-wontfix] | This will not be worked on |

## Acknowledgements
The formatting and text of portions of this _CONTRIBUTING_ document have been copied and modified from the original over at [Atom](https://github.com/atom/atom/blob/master/CONTRIBUTING.md)

[search-streamactions-repo-label-bug]: https://github.com/search?q=is%3Aopen+is%3Aissue+repo%3AStreamActions%2FStreamActions-Bot+label%3Abug
[search-streamactions-repo-label-documentation]: https://github.com/search?q=is%3Aopen+is%3Aissue+repo%3AStreamActions%2FStreamActions-Bot+label%3Adocumentation
[search-streamactions-repo-label-duplicate]: https://github.com/search?q=is%3Aopen+is%3Aissue+repo%3AStreamActions%2FStreamActions-Bot+label%3Aduplicate
[search-streamactions-repo-label-enhancement]: https://github.com/search?q=is%3Aopen+is%3Aissue+repo%3AStreamActions%2FStreamActions-Bot+label%3Aenhancement
[search-streamactions-repo-label-help-wanted]: https://github.com/search?q=is%3Aopen+is%3Aissue+repo%3AStreamActions%2FStreamActions-Bot+label%3Ahelp%20wanted
[search-streamactions-repo-label-invalid]: https://github.com/search?q=is%3Aopen+is%3Aissue+repo%3AStreamActions%2FStreamActions-Bot+label%3Ainvalid
[search-streamactions-repo-label-question]: https://github.com/search?q=is%3Aopen+is%3Aissue+repo%3AStreamActions%2FStreamActions-Bot+label%3Aquestion
[search-streamactions-repo-label-refactor]: https://github.com/search?q=is%3Aopen+is%3Aissue+repo%3AStreamActions%2FStreamActions-Bot+label%3Arefactor
[search-streamactions-repo-label-testing]: https://github.com/search?q=is%3Aopen+is%3Aissue+repo%3AStreamActions%2FStreamActions-Bot+label%3Atesting
[search-streamactions-repo-label-wontfix]: https://github.com/search?q=is%3Aopen+is%3Aissue+repo%3AStreamActions%2FStreamActions-Bot+label%3Awontfix
