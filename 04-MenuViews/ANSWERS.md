# Assignment 04 — Answers

Student name:  ibrahim ali
Group / ID:  3

## Why Expected and Actual disagree

The Views++ operation is not atomic. Multiple threads can read the same value and update it simultaneously, causing lost increments. This is called a Race Condition

## What you changed

Replaced Views++ with Interlocked.Increment(ref Views) to make the increment operation atomic and ensure accurate counting when multiple threads access the counter.

## When would you protect the update differently

use lock when an operation involves multiple shared variables or multiple statements that must execute together as one critical section.
