declare module '@tanstack/react-query' {
  export interface InvalidateQueryFilters {
    queryKey?: readonly unknown[];
  }

  export interface QueryClient {
    invalidateQueries(filters?: InvalidateQueryFilters): Promise<void>;
  }

  export function useQueryClient(): QueryClient;

  export interface UseQueryOptions<TData> {
    queryKey: readonly unknown[];
    queryFn: () => Promise<TData> | TData;
    enabled?: boolean;
    retry?: boolean;
  }

  export interface UseQueryResult<TData> {
    data: TData | undefined;
    error: Error | null;
    isLoading: boolean;
  }

  export function useQuery<TData>(options: UseQueryOptions<TData>): UseQueryResult<TData>;

  export interface UseMutationOptions<TData, TVariables> {
    mutationFn: (variables: TVariables) => Promise<TData> | TData;
    onSuccess?: (data: TData, variables: TVariables) => Promise<void> | void;
  }

  export interface UseMutationResult<TData, TVariables> {
    mutate: (variables: TVariables) => void;
    mutateAsync: (variables: TVariables) => Promise<TData>;
    data: TData | undefined;
    error: Error | null;
    isPending: boolean;
    isSuccess: boolean;
  }

  export function useMutation<TData, TError = Error, TVariables = void>(
    options: UseMutationOptions<TData, TVariables>
  ): UseMutationResult<TData, TVariables>;
}
