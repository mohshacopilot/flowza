import { Component, type ErrorInfo, type ReactNode } from 'react';
import { ErrorPage } from './ErrorPages';

interface AppErrorBoundaryState {
  error: unknown;
  hasError: boolean;
}

/**
 * Last line of defence for errors outside the router's own error boundaries (for example in a provider).
 * Logs only the error object to the console (no form values or personal data, constitution 11.2).
 */
export class AppErrorBoundary extends Component<{ children: ReactNode }, AppErrorBoundaryState> {
  override state: AppErrorBoundaryState = { error: undefined, hasError: false };

  static getDerivedStateFromError(error: unknown): AppErrorBoundaryState {
    return { error, hasError: true };
  }

  override componentDidCatch(error: unknown, info: ErrorInfo): void {
    console.error('Unhandled error in the web app', error, info.componentStack);
  }

  override render(): ReactNode {
    return this.state.hasError ? <ErrorPage error={this.state.error} /> : this.props.children;
  }
}
