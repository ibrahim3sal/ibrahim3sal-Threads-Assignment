# Assignment 05 — Answers

Student name:  ibrahim ali
Group / ID: 3

## Failures you observed

The Dictionary produced incorrect view counts because multiple threads accessed and modified it simultaneously.

The Queue caused race conditions and InvalidOperationException because multiple workers could check its count and dequeue jobs at the same time.

Some email jobs could be lost due to concurrent access.

## What you changed

Added a private lock to protect all Dictionary operations inside RecordView().

Added another lock to protect Queue operations inside PlaceOrder() and EmailWorker().

Kept a separate lock to protect the SentEmails List.

## Collection safety vs business-rule safety

Making a collection thread-safe protects individual collection operations, but it does not automatically protect a business operation involving multiple steps.

For example, checking whether a key exists and then adding or updating it must be protected as one operation to prevent race conditions.
