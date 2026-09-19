import React, { useCallback, useEffect, useMemo, useState } from 'react';
import Alert from 'Components/Alert';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import FilterMenu from 'Components/Menu/FilterMenu';
import PageMenuButton from 'Components/Menu/PageMenuButton';
import Table from 'Components/Table/Table';
import TableBody from 'Components/Table/TableBody';
import { useCustomFiltersList } from 'Filters/useCustomFilters';
import { align, kinds } from 'Helpers/Props';
import { SortDirection } from 'Helpers/Props/sortDirections';
import getErrorMessage from 'Utilities/Object/getErrorMessage';
import translate from 'Utilities/String/translate';
import InteractiveSearchFilterModal from './InteractiveSearchFilterModal';
import InteractiveSearchPayload from './InteractiveSearchPayload';
import InteractiveSearchRow from './InteractiveSearchRow';
import InteractiveSearchType from './InteractiveSearchType';
import { setReleaseOption, useReleaseOptions } from './releaseOptionsStore';
import useReleases, { FILTERS, setReleaseSort } from './useReleases';
import styles from './InteractiveSearch.css';

interface InteractiveSearchProps {
  type: InteractiveSearchType;
  searchPayload: InteractiveSearchPayload;
}

function InteractiveSearch({ type, searchPayload }: InteractiveSearchProps) {
  const customFilters = useCustomFiltersList('releases');
  const [quickFilter, setQuickFilter] = useState('');
  const [visibleCount, setVisibleCount] = useState(250);
  const { columns } = useReleaseOptions();

  const {
    isFetching,
    isFetched,
    error,
    data,
    totalItems,
    selectedFilterKey,
    sortKey,
    sortDirection,
  } = useReleases(searchPayload);

  const handleFilterSelect = useCallback(
    (selectedFilterKey: string | number) => {
      if (type === 'episode') {
        setReleaseOption('episodeSelectedFilterKey', selectedFilterKey);
      } else {
        setReleaseOption('seasonSelectedFilterKey', selectedFilterKey);
      }
    },
    [type]
  );

  const handleSortPress = useCallback(
    (sortKey: string, sortDirection?: SortDirection) => {
      setReleaseSort(sortKey, sortDirection);
    },
    []
  );

  const filteredData = useMemo(() => {
    const value = quickFilter.trim().toLowerCase();

    if (!value) {
      return data;
    }

    return data.filter((item) => item.release.title.toLowerCase().includes(value));
  }, [data, quickFilter]);

  useEffect(() => {
    setVisibleCount(250);
  }, [quickFilter, data]);

  const visibleData = filteredData.slice(0, visibleCount);
  const errorMessage = getErrorMessage(error);

  return (
    <div>
      <div className={styles.searchTools}>
        <input
          className={styles.quickFilter}
          type="search"
          value={quickFilter}
          placeholder={translate('Search')}
          aria-label={translate('Search')}
          onChange={(event) => setQuickFilter(event.currentTarget.value)}
        />

        <div className={styles.filterMenuContainer}>
          <FilterMenu
          alignMenu={align.RIGHT}
          selectedFilterKey={selectedFilterKey}
          filters={FILTERS}
          customFilters={customFilters}
          buttonComponent={PageMenuButton}
          filterModalConnectorComponent={InteractiveSearchFilterModal}
          filterModalConnectorComponentProps={{ type, searchPayload }}
          onFilterSelect={handleFilterSelect}
          />
        </div>
      </div>

      {isFetching ? <LoadingIndicator /> : null}

      {!isFetching && error ? (
        <div>
          {errorMessage ? (
            <>
              {translate('InteractiveSearchResultsSeriesFailedErrorMessage', {
                message:
                  errorMessage.charAt(0).toLowerCase() + errorMessage.slice(1),
              })}
            </>
          ) : (
            translate('EpisodeSearchResultsLoadError')
          )}
        </div>
      ) : null}

      {!isFetching && isFetched && !totalItems ? (
        <Alert kind={kinds.INFO}>{translate('NoResultsFound')}</Alert>
      ) : null}

      {!!totalItems && !isFetching && !data.length ? (
        <Alert kind={kinds.WARNING}>
          {translate('AllResultsAreHiddenByTheAppliedFilter')}
        </Alert>
      ) : null}

      {!isFetching && !!filteredData.length ? (
        <Table
          columns={columns}
          sortKey={sortKey}
          sortDirection={sortDirection}
          onSortPress={handleSortPress}
        >
          <TableBody>
            {visibleData.map((item) => {
              return (
                <InteractiveSearchRow
                  key={`${item.release.indexerId}-${item.release.guid}`}
                  {...item}
                  searchPayload={searchPayload}
                />
              );
            })}
          </TableBody>
        </Table>
      ) : null}

      {!isFetching && visibleData.length < filteredData.length ? (
        <div className={styles.loadMore}>
          <Button onPress={() => setVisibleCount((count) => count + 250)}>
            {translate('LoadMore')} ({filteredData.length - visibleData.length})
          </Button>
        </div>
      ) : null}

      {!isFetching && totalItems !== data.length && !!data.length ? (
        <div className={styles.filteredMessage}>
          {translate('SomeResultsAreHiddenByTheAppliedFilter')}
        </div>
      ) : null}
    </div>
  );
}

export default InteractiveSearch;
