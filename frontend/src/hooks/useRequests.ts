import { useCallback, useEffect, useRef, useState } from "react";
import { errorMessage, listRequests } from "../services/requestsApi";
import type { RequestFilter, ServiceRequest } from "../types/requests";

interface RequestListState {
  requests: ServiceRequest[];
  loading: boolean;
  refreshing: boolean;
  error: string | null;
  updatedAt: Date | null;
}

const REFRESH_INTERVAL = 2000;

export function useRequests(filter: RequestFilter) {
  const [reloadKey, setReloadKey] = useState(0);
  const previousFilter = useRef(filter);
  const [state, setState] = useState<RequestListState>({
    requests: [],
    loading: true,
    refreshing: false,
    error: null,
    updatedAt: null,
  });

  const refresh = useCallback(() => setReloadKey((value) => value + 1), []);

  useEffect(() => {
    const controller = new AbortController();
    let active = true;
    let timer: ReturnType<typeof setTimeout> | undefined;
    const filterChanged = previousFilter.current !== filter;
    previousFilter.current = filter;

    setState((current) => ({
      ...current,
      requests: filterChanged ? [] : current.requests,
      loading: filterChanged || current.updatedAt === null,
      refreshing: true,
      error: null,
      updatedAt: filterChanged ? null : current.updatedAt,
    }));

    async function load() {
      if (!active) {
        return;
      }

      setState((current) => ({ ...current, refreshing: true }));

      try {
        const requests = await listRequests(filter, controller.signal);

        if (active) {
          setState({
            requests,
            loading: false,
            refreshing: false,
            error: null,
            updatedAt: new Date(),
          });
        }
      } catch (error) {
        if (active && !controller.signal.aborted) {
          setState((current) => ({
            ...current,
            loading: false,
            refreshing: false,
            error: errorMessage(
              error,
              "No fue posible actualizar las solicitudes. Intenta nuevamente.",
            ),
          }));
        }
      } finally {
        // La próxima consulta espera a que termine la anterior.
        if (active) {
          timer = setTimeout(() => void load(), REFRESH_INTERVAL);
        }
      }
    }

    void load();

    return () => {
      active = false;
      controller.abort();
      clearTimeout(timer);
    };
  }, [filter, reloadKey]);

  return { ...state, refresh };
}
