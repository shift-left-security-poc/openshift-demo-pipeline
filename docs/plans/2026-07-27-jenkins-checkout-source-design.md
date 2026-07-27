# Jenkins Pipeline Source Alignment Design

## Context

The `gremlins` route serves an app version that does not match the current repository source. Investigation showed `jenkins/Jenkinsfile` checks out a different repository URL, overriding OpenShift BuildConfig source.

## Goal

Ensure the pipeline always builds the repository/ref configured in OpenShift `BuildConfig` so deployed artifacts match this repository.

## Chosen Approach

Replace the hardcoded `git 'https://github.com/adynetro/openshift-demo-pipeline.git'` step with `checkout scm` in `jenkins/Jenkinsfile`.

### Why

1. `BuildConfig.spec.source.git` remains the single source of truth for repository and branch.
2. It removes repository drift caused by hardcoded Jenkinsfile URLs.
3. It is the minimal, low-risk change and preserves existing build and binary-build behavior.

## Scope

1. Update only the checkout stage in `jenkins/Jenkinsfile`.
2. Keep npm build and OpenShift binary build stages unchanged.

## Verification Plan

1. Trigger pipeline with `oc start-build bc/gremlins-pipeline`.
2. Confirm pipeline build runs/completes.
3. Validate route content matches current `gremlins/src/App.js`.
4. If mismatch remains, trace Build -> ImageStreamTag -> DeploymentConfig digests and inspect build logs.

